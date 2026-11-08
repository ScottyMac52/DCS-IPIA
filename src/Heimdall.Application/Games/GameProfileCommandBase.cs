using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Owns availability, stale-review rejection and one admitted operation for typed native profile commands.</summary>
    /// <remarks>Sequence: availability → admission → stop input → capture fingerprint → native prepare/review → reject stale lease/fingerprint → run native once → accept only a successful result for the live lease → refresh → release.</remarks>
    public abstract class GameProfileCommandBase<TRequest, TPrepared, TResult> : IGameProfileCommand<TRequest, TResult>
    {
        private readonly IGameWorkspaceContext _context;
        private readonly IWorkspaceInputLifetime _input;
        /// <summary>Binds the command to one context and input lifetime.</summary>
        protected GameProfileCommandBase(IGameWorkspaceContext context, IWorkspaceInputLifetime input)
        {
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(input);
            _context = context; _input = input;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public WorkspaceAvailability Availability
        {
            get
            {
                var state = _context.State;
                if (state.Operation is not null) return new(false, "Another workspace operation is still running.");
                if (state.Recovery.Length > 0 && !CanRecoverWorkspace) return new(false, state.Recovery);
                var native = ReadAvailability();
                return native.Enabled || native.Reason.Length > 0 ? native : native with { Reason = "This command is unavailable." };
            }
        }
        /// <inheritdoc/>
        public async Task<ProfileCommandExecution<TResult>> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
        {
            var availability = Availability;
            if (!availability.Enabled) throw new InvalidOperationException(availability.Reason);
            var result = default(TResult);
            var outcome = await _context.RunOperationAsync(Kind, async lease =>
            {
                _input.Stop();
                var reviewed = _context.CaptureSnapshot();
                var prepared = Prepare(request);
                // Review dialogs may pump messages: the lease and fingerprint must still match before native work.
                if (!_context.OwnsOperation(lease) || _context.State.Identity != lease.Identity
                    || !string.Equals(_context.CaptureSnapshot(), reviewed, StringComparison.Ordinal))
                    throw new InvalidOperationException("The workspace changed during review. Review the command again.");
                lease.Cancellation.ThrowIfCancellationRequested();
                result = await RunNativeAsync(prepared, lease);
                await lease.ResumeOnOwnerAsync(); // Owner-thread contract: acceptance and refresh never run on a pool thread.
                if (!IsSuccessful(result)) { ReportUnsuccessful(result); return new(WorkspaceOperationOutcome.NoChange, ""); }
                var effects = ReadEffects(result);
                var committed = effects != ProfileCommandEffects.None;
                if (!_context.OwnsOperation(lease) || _context.State.Identity != lease.Identity)
                    return new(WorkspaceOperationOutcome.Applied, committed ? "The operation completed before the workspace closed; retain the native receipt." : "", committed);
                try { Accept(result, lease); _context.RefreshNativeState(); }
                catch (Exception error) when (committed && error is not (OutOfMemoryException or InsufficientExecutionStackException or AccessViolationException))
                {
                    var recovery = effects == ProfileCommandEffects.WorkspaceWrite;
                    var message = "The operation completed, but its result could not be shown (" + error.Message + "). "
                        + (recovery ? "Reopen the saved configuration before continuing; do not repeat the write." : "Keep the output receipt; do not repeat the export.");
                    _context.ReportStatus(message);
                    return new(WorkspaceOperationOutcome.Applied, message, true, recovery);
                }
                return new(WorkspaceOperationOutcome.Applied, "", committed);
            }, cancellationToken);
            if (outcome.Outcome == WorkspaceOperationOutcome.Cancelled) throw new OperationCanceledException(outcome.Message);
            if (outcome.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(outcome.Message);
            return new(result!, outcome);
        }
        #endregion END Public
        /// <summary>Gets whether this command imports a fresh session to recover the workspace; creation and exports remain blocked.</summary>
        protected virtual bool CanRecoverWorkspace => false;
        /// <summary>Gets the operation kind admitted for this command.</summary>
        protected abstract WorkspaceOperationKind Kind { get; }
        /// <summary>Reads native edition, platform and source availability.</summary>
        protected abstract WorkspaceAvailability ReadAvailability();
        /// <summary>Prepares and reviews native input inside the admitted operation; a picker/review cancellation is a typed unsuccessful result, not an exception.</summary>
        protected abstract TPrepared Prepare(TRequest request);
        /// <summary>Runs the native command once for the admitted lease.</summary>
        protected abstract Task<TResult> RunNativeAsync(TPrepared prepared, IWorkspaceOperationLease lease);
        /// <summary>Gets whether the native result should be accepted.</summary>
        protected abstract bool IsSuccessful(TResult result);
        /// <summary>Declares whether successful native work wrote a workspace or output; import/unsaved copies return None.</summary>
        protected abstract ProfileCommandEffects ReadEffects(TResult result);
        /// <summary>Reports native cancellation or failure without accepting it.</summary>
        protected virtual void ReportUnsuccessful(TResult result) { }
        /// <summary>Accepts a successful native result into the live workspace.</summary>
        protected abstract void Accept(TResult result, IWorkspaceOperationLease lease);
    }
}
