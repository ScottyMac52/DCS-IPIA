using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Exposes current editing state and an exact draft snapshot without coupling the workflow to a persistence model.</summary>
    public interface IWorkspaceAssignmentHost
    {
        #region BEGIN Public
        /// <summary>Gets fresh workspace, scope and editing gates each time an operation is checked.</summary>
        WorkspaceAssignmentContext Context { get; }
        /// <summary>Captures stable ordered draft state for actual change detection, independently of saved dirty state.</summary>
        string CaptureSnapshot();
        #endregion END Public
    }
}
