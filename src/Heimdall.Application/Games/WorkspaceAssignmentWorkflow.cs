using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Shared execution boundary for assignment entry points: fresh gates, stable target scope and actual draft change detection.</summary>
    public sealed class WorkspaceAssignmentWorkflow
    {
        private int _executing;
        #region BEGIN Public
        /// <summary>Occurs once for a completed changed, unchanged or blocked action; consumers refresh from the resulting draft.</summary>
        public event EventHandler<DraftResult>? Completed;
        /// <summary>Gets the immutable target of the most recently requested action.</summary>
        public WorkspaceAssignmentTarget? Selection { get; private set; }
        /// <summary>Adapts a native void mutation to the same review, state and actual-change boundary used by DCS drafts.</summary>
        public DraftResult Execute(IWorkspaceAssignmentHost host, WorkspaceAssignmentTarget target,
            Func<GameAssignmentReview> review, Action apply)
        {
            ArgumentNullException.ThrowIfNull(review); ArgumentNullException.ThrowIfNull(apply);
            return Execute(host, new WorkspaceAssignmentOperation(target, review, () =>
            {
                apply();
                return new(DraftOutcome.Applied, "");
            }));
        }
        /// <summary>Checks fresh state and adapter review, then executes one action without persisting or deploying files.</summary>
        public DraftResult Execute(IWorkspaceAssignmentHost host, IWorkspaceAssignmentOperation operation)
        {
            ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(operation);
            if (Interlocked.CompareExchange(ref _executing, 1, 0) != 0) return new(DraftOutcome.Blocked, "Another assignment operation is already running.");
            try
            {
                Selection = operation.Target;
                var result = Run(host, operation);
                if (result.Outcome == DraftOutcome.Blocked || host.Context.Scope != operation.Target.Scope) Selection = null;
                Completed?.Invoke(this, result);
                return result;
            }
            catch { Selection = null; throw; }
            finally { Volatile.Write(ref _executing, 0); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DraftResult Run(IWorkspaceAssignmentHost host, IWorkspaceAssignmentOperation operation)
        {
            var blocked = Check(host.Context, operation.Target);
            if (blocked is not null) return blocked;
            var before = host.CaptureSnapshot();
            var review = operation.Review();
            blocked = Check(host.Context, operation.Target);
            if (blocked is not null) return blocked;
            if (host.CaptureSnapshot() != before) return new(DraftOutcome.Blocked, "The draft changed during review. Review the current assignment again.");
            if (!review.Valid) return new(DraftOutcome.Blocked, review.Message);
            if (!review.Changed) return new(DraftOutcome.NoChange, "Assignment unchanged.");
            var result = operation.Apply();
            if (result.Outcome != DraftOutcome.Applied) return result;
            return host.CaptureSnapshot() == before
                ? new(DraftOutcome.NoChange, "Assignment unchanged.")
                : result with { Message = result.Message.Length == 0 ? "Assignment changed and staged. Save before exporting." : result.Message };
        }
        private static DraftResult? Check(WorkspaceAssignmentContext context, WorkspaceAssignmentTarget target)
        {
            if (context.BlockedReason.Length != 0) return new(DraftOutcome.Blocked, context.BlockedReason);
            return string.IsNullOrWhiteSpace(target.Scope) || context.Scope != target.Scope
                ? new(DraftOutcome.Blocked, "The assignment target changed. Select the current controller and input again.") : null;
        }
        #endregion END private helpers
    }
}

