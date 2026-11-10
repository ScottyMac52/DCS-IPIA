using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Counts durable writes made through one workspace's view of a real store.</summary>
    public sealed class CountingWorkspaceStore(IGameWorkspaceStore inner) : IGameWorkspaceStore
    {
        #region BEGIN Public
        /// <summary>Gets the durable writes made through this counter.</summary>
        public int Writes { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyList<GameWorkspaceEntry> List() => inner.List();
        /// <inheritdoc/>
        public GameWorkspaceRead Load(Guid id) => inner.Load(id);
        /// <inheritdoc/>
        public string Save(GameWorkspace workspace, string? expectedRevision) { var revision = inner.Save(workspace, expectedRevision); Writes++; return revision; }
        /// <inheritdoc/>
        public void Delete(Guid id, string expectedRevision) => inner.Delete(id, expectedRevision);
        /// <inheritdoc/>
        public GameSelectionState ReadSelection() => inner.ReadSelection();
        /// <inheritdoc/>
        public void SaveSelection(GameSelectionState selection) => inner.SaveSelection(selection);
        #endregion END Public
    }
}
