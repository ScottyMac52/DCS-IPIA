using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Temporary typed engine bridge for native browser migration; removed when PRs 4/9 eliminate direct mutation.</summary>
    public interface IWorkspaceSelectionEngineOwner
    {
        #region BEGIN Public
        /// <summary>Gets the single context-owned engine, never a view-created copy.</summary>
        GameAssignmentSelection SelectionEngine { get; }
        #endregion END Public
    }
}
