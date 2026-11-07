using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Counts durable profile writes over the real file store so smokes can prove exactly one persist per accepted save.</summary>
    internal sealed class CountingGameWorkspaceStore(IGameWorkspaceStore inner) : IGameWorkspaceStore
    {
        internal int Writes { get; private set; }
        internal Exception? Fail { get; set; }
        public IReadOnlyList<GameWorkspaceEntry> List() => inner.List();
        public GameWorkspaceRead Load(Guid id) => inner.Load(id);
        public string Save(GameWorkspace workspace, string? expectedRevision)
        {
            if (Fail is { } fail) throw fail;
            var revision = inner.Save(workspace, expectedRevision); Writes++; return revision;
        }
        public void Delete(Guid id, string expectedRevision) => inner.Delete(id, expectedRevision);
        public GameSelectionState ReadSelection() => inner.ReadSelection();
        public void SaveSelection(GameSelectionState selection) => inner.SaveSelection(selection);
    }
}
