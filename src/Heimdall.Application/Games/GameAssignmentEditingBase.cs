using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Owns assignment admission and feedback around the context's existing workflow.</summary>
    public abstract class GameAssignmentEditingBase(Func<IGameWorkspaceContext?> readContext) : IGameAssignmentEditing
    {
        private bool _disposed;
        #region BEGIN Public
        /// <inheritdoc/>
        public event EventHandler<DraftResult>? Completed;
        /// <inheritdoc/>
        public WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action) =>
            ActionState(action, ReadNativeSupport(action));
        /// <inheritdoc/>
        public WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action, WorkspaceAssignmentActionSupport target) =>
            WorkspaceAssignmentActionState.For(action, CurrentContext(), target);
        /// <inheritdoc/>
        public DraftResult Execute(WorkspaceAssignmentAction action, IWorkspaceAssignmentOperation operation)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(operation);
            if (readContext() is not { } context)
                return new(DraftOutcome.Blocked, "Open a workspace before editing.");

#if PERFORMANCE_TRACE
            using var performance = PerformanceTrace.Measure("assignment.execute", GetType());
#endif
            var host = new WorkspaceAssignmentHost(() => ReadAssignmentContext(context), context.CaptureSnapshot);
            WorkspaceSelection? reviewedSelection = null;
            DraftResult? CheckCurrentTarget(WorkspaceAssignmentContext state)
            {
                if (_disposed || !ReferenceEquals(readContext(), context))
                    return new(DraftOutcome.Blocked, "The workspace changed during review.");
                reviewedSelection ??= context.State.Selection;
                if (context.State.Selection != reviewedSelection)
                    return new(DraftOutcome.Blocked, "The selection changed during review.");
                return CheckAction(action, state);
            }
            var admitted = false;
            DraftResult result;
            try
            {
                using var refresh = context.DeferRefresh();
                result = context.AssignmentWorkflow.Execute(host, operation, CheckCurrentTarget, completion =>
                {
                    admitted = true;
                    Completed?.Invoke(this, completion);
                });
            }
            catch
            {
                RefreshActions();
                throw;
            }
            if (admitted)
            {
                RefreshActions();
                ReportNativeResult(result);
            }
            return result;
        }
        /// <inheritdoc/>
        public void RefreshActions()
        {
            if (!_disposed) RefreshNativeEditor();
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            _disposed = true;
            Completed = null;
            GC.SuppressFinalize(this);
        }
        #endregion END Public

        /// <summary>Reads the adapter capability without applying a mutation.</summary>
        protected abstract WorkspaceAssignmentActionSupport ReadNativeSupport(WorkspaceAssignmentAction action);
        /// <summary>Refreshes native editor availability after the workflow.</summary>
        protected abstract void RefreshNativeEditor();
        /// <summary>Displays the authoritative result.</summary>
        protected abstract void ReportNativeResult(DraftResult result);
        /// <summary>Synchronizes any native selection before reading the common context.</summary>
        protected virtual WorkspaceAssignmentContext ReadAssignmentContext(IGameWorkspaceContext context) => context.Context;
        /// <summary>Captures a native target before an action or dialog starts.</summary>
        protected WorkspaceAssignmentTarget CaptureTarget(string command, string input, string alternative) =>
            new(CurrentContext().Scope, command, input, alternative);
        /// <summary>Executes an adapter draft whose mutation returns native feedback.</summary>
        protected DraftResult ExecuteNative(WorkspaceAssignmentAction action, string command, string input, string alternative,
            Func<GameAssignmentReview> review, Func<DraftResult> apply) =>
            Execute(action, new WorkspaceAssignmentOperation(CaptureTarget(command, input, alternative), review, apply));
        /// <summary>Executes an adapter draft whose native write either succeeds or throws.</summary>
        protected DraftResult ExecuteNative(WorkspaceAssignmentAction action, string command, string input, string alternative,
            Func<GameAssignmentReview> review, Action apply) =>
            ExecuteNative(action, command, input, alternative, review, () => { apply(); return new(DraftOutcome.Applied, ""); });

        private WorkspaceAssignmentContext CurrentContext() =>
            !_disposed && readContext() is { } context ? ReadAssignmentContext(context) : new("", false, false, false, false);

        private DraftResult? CheckAction(WorkspaceAssignmentAction action, WorkspaceAssignmentContext state)
        {
            var availability = WorkspaceAssignmentActionState.For(action, state, ReadNativeSupport(action));
            return availability.Enabled ? null : new(DraftOutcome.Blocked, availability.Reason);
        }
    }
}
