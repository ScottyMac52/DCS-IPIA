using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Counts DCS repository writes over the real SQLite repository.</summary>
    internal sealed class CountingDcsRepository(IDcsEditorWorkspaceRepository inner) : IDcsEditorWorkspaceRepository
    {
        internal int Writes { get; private set; }
        internal Exception? Fail { get; set; }
        public Task<DcsEditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default) => inner.GetAsync(moduleId, ct);
        public Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public async Task SaveAsync(DcsEditorWorkspaceSnapshot snapshot, CancellationToken ct = default)
        {
            if (Fail is { } fail) throw fail;
            await inner.SaveAsync(snapshot, ct); Writes++;
        }
        public Task<bool> DeleteAsync(Guid moduleId, CancellationToken ct = default) => inner.DeleteAsync(moduleId, ct);
    }
}
