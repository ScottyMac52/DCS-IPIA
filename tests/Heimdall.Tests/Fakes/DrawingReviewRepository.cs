using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests.Fakes
{
    /// <summary>Deterministic reviewed storage with stale-write checking and an optional failing module.</summary>
    internal sealed class DrawingReviewRepository(IEnumerable<EditorWorkspaceSnapshot> snapshots, bool includeMissingModule = false)
        : IEditorWorkspaceRepository, IEditorWorkspaceUpdateRepository
    {
        private readonly Dictionary<Guid, EditorWorkspaceSnapshot> _snapshots = snapshots.ToDictionary(x => x.Configuration.Module.Id);
        #region BEGIN Public
        /// <summary>Gets or sets the module whose reviewed write should fail.</summary>
        public Guid? FailModule { get; init; }
        /// <summary>Gets committed writes.</summary>
        public int WriteCount { get; private set; }
        /// <inheritdoc/>
        public Task<EditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default) => Task.FromResult(_snapshots.GetValueOrDefault(moduleId));
        /// <inheritdoc/>
        public Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(includeMissingModule ? _snapshots.Keys.Append(Guid.Empty).ToArray() : _snapshots.Keys.ToArray());
        /// <inheritdoc/>
        public Task SaveAsync(EditorWorkspaceSnapshot snapshot, CancellationToken ct = default)
        {
            _snapshots[snapshot.Configuration.Module.Id] = snapshot;
            WriteCount++;
            return Task.CompletedTask;
        }
        /// <inheritdoc/>
        public Task<bool> DeleteAsync(Guid moduleId, CancellationToken ct = default) => Task.FromResult(_snapshots.Remove(moduleId));
        /// <inheritdoc/>
        public Task SaveIfUnchangedAsync(EditorWorkspaceSnapshot expected, EditorWorkspaceSnapshot updated, CancellationToken ct = default)
        {
            if (FailModule == expected.Configuration.Module.Id || !ReferenceEquals(_snapshots[expected.Configuration.Module.Id], expected))
                throw new InvalidOperationException("Reviewed module changed.");
            return SaveAsync(updated, ct);
        }
        /// <inheritdoc/>
        public Task<EditorWorkspaceSnapshot?> GetPriorAsync(Guid moduleId, CancellationToken ct = default) => Task.FromResult<EditorWorkspaceSnapshot?>(null);
        #endregion END Public
    }
}
