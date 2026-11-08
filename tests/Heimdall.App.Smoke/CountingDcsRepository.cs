using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Counts DCS repository writes over the real SQLite repository.</summary>
    internal sealed class CountingDcsRepository(IDcsEditorWorkspaceRepository inner) : IDcsEditorWorkspaceRepository
    {
        #region BEGIN Public
        /// <summary>Gets the durable writes made through this game's counter.</summary>
        internal int Writes { get; private set; }
        /// <summary>Gets or sets a failure thrown before the next write.</summary>
        internal Exception? Fail { get; set; }
        /// <inheritdoc/>
        public Task<DcsEditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default) => inner.GetAsync(moduleId, ct);
        /// <inheritdoc/>
        public Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        /// <inheritdoc/>
        public async Task SaveAsync(DcsEditorWorkspaceSnapshot snapshot, CancellationToken ct = default)
        {
            if (Fail is { } fail) throw fail;
            await inner.SaveAsync(snapshot, ct); Writes++;
        }
        /// <inheritdoc/>
        public Task<bool> DeleteAsync(Guid moduleId, CancellationToken ct = default) => inner.DeleteAsync(moduleId, ct);
        #endregion END Public
    }
}
