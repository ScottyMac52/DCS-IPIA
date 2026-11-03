using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Binds adapter-native review and mutation to an immutable target; no WPF or simulator parsing enters the shared workflow.</summary>
    public sealed class WorkspaceAssignmentOperation(WorkspaceAssignmentTarget target, Func<GameAssignmentReview> review, Func<DraftResult> apply) : IWorkspaceAssignmentOperation
    {
        #region BEGIN Public
        /// <summary>Gets the bound target, never re-read from mutable UI controls during execution.</summary>
        public WorkspaceAssignmentTarget Target { get; } = target;
        /// <summary>Runs the adapter's non-mutating operation review.</summary>
        public GameAssignmentReview Review() => review();
        /// <summary>Stages the reviewed native operation through its existing draft facade.</summary>
        public DraftResult Apply() => apply();
        #endregion END Public
    }
}
