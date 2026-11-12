using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Runs native assignment operations through the workspace's editing policy.</summary>
    public interface IGameAssignmentEditing : IDisposable
    {
        #region BEGIN Public
        /// <summary>Publishes the admitted operation result once.</summary>
        event EventHandler<DraftResult>? Completed;
        /// <summary>Combines the adapter capability with current workspace gates.</summary>
        WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action);
        /// <summary>Combines a pointed or selected native target with current workspace gates.</summary>
        WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action, WorkspaceAssignmentActionSupport target);
        /// <summary>Runs a reviewed native assignment operation.</summary>
        DraftResult Execute(WorkspaceAssignmentAction action, IWorkspaceAssignmentOperation operation);
        /// <summary>Refreshes native action and editor presentation.</summary>
        void RefreshActions();
        #endregion END Public
    }
}
