namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Stores graphical editor data and assignments in the local database.</summary>
    public interface IEditorWorkspaceRepository
    {
        #region BEGIN Public
        /// <summary>Loads a persisted module editor without reading repositories.</summary>
        /// <param name="moduleId">Module identity.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The saved workspace, or null when it does not exist.</returns>
        Task<EditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default);
        /// <summary>Lists stored editor workspaces.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Saved module identifiers.</returns>
        Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default);
        /// <summary>Atomically replaces the editor snapshot, including staged assignments.</summary>
        /// <param name="snapshot">Complete validated workspace.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task completing after the transaction commits.</returns>
        Task SaveAsync(EditorWorkspaceSnapshot snapshot, CancellationToken ct = default);
        #endregion END Public
    }
}
