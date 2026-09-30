namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Persists shared default revisions without updating module workspaces.</summary>
    public interface ISharedUiLayerWorkspaceRepository
    {
        #region BEGIN Public
        /// <summary>Loads the current or a historical default revision.</summary>
        /// <param name="revision">Historical revision, or null for latest.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Stored defaults, or null when absent.</returns>
        Task<SharedUiLayerWorkspace?> GetAsync(int? revision = null, CancellationToken ct = default);
        /// <summary>Appends a revision only when the caller's baseline is current.</summary>
        /// <param name="workspace">Defaults carrying the expected current revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>New stored revision and content identity.</returns>
        Task<SharedUiLayerWorkspace> SaveAsync(SharedUiLayerWorkspace workspace, CancellationToken ct = default);
        #endregion END Public
    }
}
