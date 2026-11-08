using System.Diagnostics;
using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Owns the one profile save/discard sequence for every game; native adapters supply only typed hooks.</summary>
    /// <remarks>
    /// Save: capability → admission → stop input → prepare (all fallible native validation) → persist once → accept → refresh → Applied/Committed → status.
    /// After persistence nothing is reported as uncommitted: an accept failure is Committed with RecoveryRequired, a refresh failure a warning.
    /// Discard: capability → admission → stop input → restore → refresh → Applied. Hook failures before commit propagate
    /// so existing native error surfaces keep reporting them; the context releases the operation and retains pending state.
    /// </remarks>
    public abstract class GameProfileOperationsBase<TSnapshot, TCommit> : IGameProfileOperations where TSnapshot : class
    {
        private readonly IGameWorkspaceContext<TSnapshot> _context;
        private readonly IWorkspaceInputLifetime _input;
        private bool _disposed;
        /// <summary>Binds the shared sequence to one context and input lifetime.</summary>
        protected GameProfileOperationsBase(IGameWorkspaceContext<TSnapshot> context, IWorkspaceInputLifetime input)
        {
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(input);
            _context = context; _input = input;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public IGameWorkspaceContext Context => _context;
        /// <inheritdoc/>
        public async Task<WorkspaceOperationResult> SaveAsync(CancellationToken cancellationToken)
        {
            if (Precheck(WorkspaceOperationKind.Save) is { } early) return early;
            return await _context.RunOperationAsync(WorkspaceOperationKind.Save, async lease =>
            {
                _input.Stop();
                lease.Cancellation.ThrowIfCancellationRequested();
                // Prepare and persist are adjacent: a native prepare may hold an editing lock released by its persist hook.
                var candidate = PrepareSave();
                var commit = await PersistAsync(candidate, lease);
                await lease.ResumeOnOwnerAsync(); // Owner-thread contract: native acceptance and status never run on a pool thread.
                // A durable write cannot be undone by cancellation; a closed or replaced workspace only skips installation.
                if (!_context.OwnsOperation(lease) || _context.State.Identity != lease.Identity)
                    return new(WorkspaceOperationOutcome.Applied, SavedMessage + " The workspace closed before the saved profile could be shown.", true);
                // Post-commit boundary: the write is durable, so no later native failure may be reported as an uncommitted save.
                try { AcceptCommit(commit, lease); }
                catch (Exception error) when (!IsProcessFatal(error))
                {
                    Trace.TraceError("Profile saved but its native baseline could not be installed: {0}", error);
                    var recovery = SavedMessage.Length == 0 ? "Profile saved." : SavedMessage;
                    var message = recovery + " The saved profile could not be shown (" + error.Message + "). Reopen it before editing further.";
                    _context.ReportStatus(message);
                    return new(WorkspaceOperationOutcome.Applied, message, true, RecoveryRequired: true);
                }
                return Finish(WorkspaceOperationKind.Save);
            }, cancellationToken);
        }
        /// <inheritdoc/>
        public Task<WorkspaceOperationResult> DiscardAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Cancelled, "Operation cancelled."));
            if (Precheck(WorkspaceOperationKind.Discard) is { } early) return Task.FromResult(early);
            return Task.FromResult(_context.RunOperation(WorkspaceOperationKind.Discard, lease =>
            {
                _input.Stop();
                RestoreSaved(lease);
                return Finish(WorkspaceOperationKind.Discard);
            }));
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; DisposeNative(); GC.SuppressFinalize(this);
        }
        #endregion END Public
        /// <summary>Gets the native status after a durable save.</summary>
        protected abstract string SavedMessage { get; }
        /// <summary>Gets the native status after a discard.</summary>
        protected abstract string DiscardedMessage { get; }
        /// <summary>Builds and validates the native candidate from the current working state and edited name; throw to reject.</summary>
        protected abstract TSnapshot PrepareSave();
        /// <summary>Performs the one native durable write for the admitted lease.</summary>
        protected abstract Task<WorkspaceCommit<TSnapshot, TCommit>> PersistAsync(TSnapshot candidate, IWorkspaceOperationLease lease);
        /// <summary>Installs the durable candidate and receipt as the native baseline. The lease authorizes native session acceptance.</summary>
        protected abstract void AcceptCommit(WorkspaceCommit<TSnapshot, TCommit> commit, IWorkspaceOperationLease lease);
        /// <summary>Restores the native saved baseline without writing; throw before swapping state to retain the draft.</summary>
        protected abstract void RestoreSaved(IWorkspaceOperationLease lease);
        /// <summary>Refreshes native panels and best-effort preferences after an accepted save or discard.</summary>
        protected abstract void RefreshAfterPersistence(WorkspaceOperationKind completed);
        /// <summary>Releases native resources owned by the adapter.</summary>
        protected virtual void DisposeNative() { }
        /// <summary>Only process-corrupting failures escape after a durable write; everything else is reported against the committed save.</summary>
        private static bool IsProcessFatal(Exception error) => error is OutOfMemoryException or InsufficientExecutionStackException or AccessViolationException;
        private WorkspaceOperationResult? Precheck(WorkspaceOperationKind kind)
        {
            var state = _context.State;
            if (state.Operation is not null) return new(WorkspaceOperationOutcome.Blocked, "Another workspace operation is still running.");
            if (!state.Open) return new(WorkspaceOperationOutcome.Blocked, "Open a workspace before continuing.");
            _context.RefreshNativeState(); state = _context.State;
            // Dirty or read-only saves go to context admission, the single owner of native restriction reasons
            // (for example an edition entitlement); admission blocks before any hook runs, so nothing is written.
            if (state.Dirty || kind == WorkspaceOperationKind.Save && state.ReadOnly) return null;
            // Clean: an unsaveable workspace is Blocked rather than reporting NoChange. Activities suppress UI capabilities,
            // so only an idle state's capability is authoritative here; admission rechecks native facts for dirty work.
            var capability = kind == WorkspaceOperationKind.Save ? state.Capabilities.Save : state.Capabilities.Discard;
            if (state.Activity is null && !capability.Enabled)
                return new(WorkspaceOperationOutcome.Blocked, string.IsNullOrEmpty(capability.Reason) ? "This operation is unavailable." : capability.Reason);
            return new(WorkspaceOperationOutcome.NoChange, kind == WorkspaceOperationKind.Save ? "No profile changes to save." : "No profile changes to discard.");
        }
        private WorkspaceOperationResult Finish(WorkspaceOperationKind kind)
        {
            var warning = "";
            try { RefreshAfterPersistence(kind); }
            catch (Exception error) when (!IsProcessFatal(error))
            {
                // The native outcome is already final; a refresh failure is reported as a warning and never retried as a save.
                Trace.TraceWarning("Profile refresh after {0} failed: {1}", kind, error);
                warning = "Warning: " + error.Message;
            }
            // Messages are read after refresh so native preference results (for example a recent-selection failure) are included.
            var message = string.Join(" ", new[] { kind == WorkspaceOperationKind.Save ? SavedMessage : DiscardedMessage, warning }.Where(x => x.Length != 0));
            // An empty native message keeps the adapter's existing status surface untouched.
            if (message.Length != 0) _context.ReportStatus(message);
            return new(WorkspaceOperationOutcome.Applied, message, kind == WorkspaceOperationKind.Save);
        }
    }
}
