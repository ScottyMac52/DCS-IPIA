using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Delays a real SQLite save so WPF interaction checks can exercise its pending interval.</summary>
    internal sealed class PausedDcsRepository(IDcsEditorWorkspaceRepository inner) : IDcsEditorWorkspaceRepository
    {
        private TaskCompletionSource? _release;
        private bool _fail;

        #region BEGIN Public
        internal int Writes { get; private set; }
        internal void Pause(bool fail)
        {
            _fail = fail;
            _release = new TaskCompletionSource();
        }
        internal void Release() => _release!.TrySetResult();
        /// <inheritdoc/>
        public Task<DcsEditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default) => inner.GetAsync(moduleId, ct);
        /// <inheritdoc/>
        public Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        /// <inheritdoc/>
        public Task<bool> DeleteAsync(Guid moduleId, CancellationToken ct = default) => inner.DeleteAsync(moduleId, ct);
        /// <inheritdoc/>
        public async Task SaveAsync(DcsEditorWorkspaceSnapshot snapshot, CancellationToken ct = default)
        {
            Writes++;
            if (_release is not null) await _release.Task;
            if (_fail) throw new System.IO.IOException("Expected delayed save failure.");
            await inner.SaveAsync(snapshot, ct);
        }
        #endregion END Public
    }
}
