using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Adapter-prepared assignment action. Review is non-mutating and Apply stages through the adapter's atomic draft boundary.</summary>
    public interface IWorkspaceAssignmentOperation
    {
        #region BEGIN Public
        /// <summary>Gets the exact target captured before any native review or user confirmation.</summary>
        WorkspaceAssignmentTarget Target { get; }
        /// <summary>Checks native compatibility and supported operations without publishing a draft mutation.</summary>
        GameAssignmentReview Review();
        /// <summary>Applies one complete edit or returns a non-applied result without mutation; native exceptions remain visible to the host.</summary>
        DraftResult Apply();
        #endregion END Public
    }
}
