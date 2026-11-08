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
                var native = ReadAvailability();
                return native.Enabled || native.Reason.Length > 0 ? native : native with { Reason = "This command is unavailable." };
            }
        }
        /// <inheritdoc/>
        public async Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
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
                if (!IsSuccessful(result)) return new(WorkspaceOperationOutcome.NoChange, "");
                if (_context.OwnsOperation(lease) && _context.State.Identity == lease.Identity) { Accept(result, lease); _context.RefreshNativeState(); }
                return new(WorkspaceOperationOutcome.Applied, "");
            }, cancellationToken);
            if (outcome.Outcome == WorkspaceOperationOutcome.Cancelled) throw new OperationCanceledException(outcome.Message);
            if (outcome.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(outcome.Message);
            return result!;
        }
        #endregion END Public
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
        /// <summary>Accepts a successful native result into the live workspace.</summary>
        protected abstract void Accept(TResult result, IWorkspaceOperationLease lease);
    }
}
