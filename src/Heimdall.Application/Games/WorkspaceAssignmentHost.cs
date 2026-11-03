using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Adapts existing portable and DCS draft facades to the common workflow without copying their storage rules.</summary>
    public sealed class WorkspaceAssignmentHost(Func<WorkspaceAssignmentContext> context, Func<string> snapshot) : IWorkspaceAssignmentHost
    {
        #region BEGIN Public
        /// <summary>Gets current live state rather than a cached action-bar decision.</summary>
        public WorkspaceAssignmentContext Context => context();
        /// <summary>Reads an independent draft token from the owning facade.</summary>
        public string CaptureSnapshot() => snapshot();
        #endregion END Public
    }
}
