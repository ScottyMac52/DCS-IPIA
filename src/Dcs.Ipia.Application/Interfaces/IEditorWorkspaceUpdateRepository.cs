namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Commits reviewed module updates with stale-state rejection and retained rollback evidence.</summary>
    public interface IEditorWorkspaceUpdateRepository
    {
        #region BEGIN Public
        /// <summary>Saves a reviewed change only when configuration and editor payload still match the expected module.</summary>
        /// <param name="expected">Previously reviewed module snapshot.</param>
        /// <param name="updated">Validated replacement for the same module.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task completing after atomic save and history capture.</returns>
        Task SaveIfUnchangedAsync(EditorWorkspaceSnapshot expected, EditorWorkspaceSnapshot updated, CancellationToken ct = default);
        /// <summary>Reads the last retained module state before a reviewed update.</summary>
        /// <param name="moduleId">Module identity.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Prior state or null when no history exists.</returns>
        Task<EditorWorkspaceSnapshot?> GetPriorAsync(Guid moduleId, CancellationToken ct = default);
        #endregion END Public
    }
}

