using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Intercepts real profile persistence to exercise reentrancy and failure without touching active game files.</summary>
    internal sealed class InterceptedGameWorkspaceStore(IGameWorkspaceStore inner) : IGameWorkspaceStore
    {
        #region BEGIN Public
        internal Action? BeforeSave { get; set; }
        internal bool FailSave { get; set; }
        internal int Writes { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyList<GameWorkspaceEntry> List() => inner.List();
        /// <inheritdoc/>
        public GameWorkspaceRead Load(Guid id) => inner.Load(id);
        /// <inheritdoc/>
        public string Save(GameWorkspace workspace, string? expectedRevision)
        {
            Writes++; BeforeSave?.Invoke();
            if (FailSave) throw new System.IO.IOException("Synthetic workspace save failure");
            return inner.Save(workspace, expectedRevision);
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
