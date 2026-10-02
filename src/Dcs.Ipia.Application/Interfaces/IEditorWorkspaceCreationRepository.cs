namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Creates complete module workspaces without replacing existing identities.</summary>
    public interface IEditorWorkspaceCreationRepository : IEditorWorkspaceRepository
    {
        #region BEGIN Public
        /// <summary>Atomically inserts a workspace only when its module and DCS folder identities are unused.</summary>
        /// <param name="snapshot">Verified workspace to restore.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Completion after the new workspace commits.</returns>
        Task CreateAsync(EditorWorkspaceSnapshot snapshot, CancellationToken ct = default);
        #endregion END Public
    }
}
