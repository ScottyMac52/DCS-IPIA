using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Tests.Fakes
{
    /// <summary>Exposes saved drawings without the reviewed-write capability.</summary>
    internal sealed class ReadOnlyDrawingRepository(EditorWorkspaceSnapshot snapshot) : IEditorWorkspaceRepository
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public Task<EditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default) => Task.FromResult<EditorWorkspaceSnapshot?>(snapshot);
        /// <inheritdoc/>
        public Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([snapshot.Configuration.Module.Id]);
        /// <inheritdoc/>
        public Task SaveAsync(EditorWorkspaceSnapshot value, CancellationToken ct = default) => throw new NotSupportedException();
        /// <inheritdoc/>
        public Task<bool> DeleteAsync(Guid moduleId, CancellationToken ct = default) => throw new NotSupportedException();
        #endregion END Public
    }
}
