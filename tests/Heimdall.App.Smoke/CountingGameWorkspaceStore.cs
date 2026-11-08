using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Counts durable profile writes over the real file store so smokes can prove exactly one persist per accepted save.</summary>
    internal sealed class CountingGameWorkspaceStore(IGameWorkspaceStore inner) : IGameWorkspaceStore
    {
        #region BEGIN Public
        /// <summary>Gets the durable writes made through this game's counter.</summary>
        internal int Writes { get; private set; }
        /// <summary>Gets or sets a failure thrown before the next write.</summary>
        internal Exception? Fail { get; set; }
        /// <inheritdoc/>
        public IReadOnlyList<GameWorkspaceEntry> List() => inner.List();
        /// <inheritdoc/>
        public GameWorkspaceRead Load(Guid id) => inner.Load(id);
        /// <inheritdoc/>
        public string Save(GameWorkspace workspace, string? expectedRevision)
        {
            if (Fail is { } fail) throw fail;
            var revision = inner.Save(workspace, expectedRevision); Writes++; return revision;
        }
        /// <inheritdoc/>
        public void Delete(Guid id, string expectedRevision) => inner.Delete(id, expectedRevision);
        /// <inheritdoc/>
        public GameSelectionState ReadSelection() => inner.ReadSelection();
        /// <inheritdoc/>
        public void SaveSelection(GameSelectionState selection) => inner.SaveSelection(selection);
        #endregion END Public
    }
}
