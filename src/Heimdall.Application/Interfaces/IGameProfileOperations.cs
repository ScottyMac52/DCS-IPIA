using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Common profile persistence role: every window, feature and leave route saves or discards through this one entry.</summary>
    public interface IGameProfileOperations : IDisposable
    {
        #region BEGIN Public
        /// <summary>Gets the single context whose operation lease guards persistence.</summary>
        IGameWorkspaceContext Context { get; }
        /// <summary>Persists a dirty workspace once. Blocked/NoChange never write; persistence failures throw and retain pending state.</summary>
        Task<WorkspaceOperationResult> SaveAsync(CancellationToken cancellationToken);
        /// <summary>Restores the native baseline without writing. Blocked/NoChange leave the workspace unchanged.</summary>
        Task<WorkspaceOperationResult> DiscardAsync(CancellationToken cancellationToken);
        #endregion END Public
    }
}
