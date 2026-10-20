using Dcs.Ipia.Application.Games;

namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Profile-owned atomic game persistence, separate from active game files.</summary>
    public interface IGameWorkspaceStore
    {
        #region BEGIN Public
        /// <summary>Lists validated local game workspaces.</summary>
        IReadOnlyList<GameWorkspaceEntry> List();
        /// <summary>Loads an independent workspace and its revision.</summary>
        GameWorkspaceRead Load(Guid id);
        /// <summary>Saves atomically; null revision creates a new identity and stale revisions fail.</summary>
        string Save(GameWorkspace workspace, string? expectedRevision);
        /// <summary>Reads the last explicit selection, or the DCS default when none exists.</summary>
        GameSelectionState ReadSelection();
        /// <summary>Saves a supported selection without opening or deploying a game.</summary>
        void SaveSelection(GameSelectionState selection);
        #endregion END Public
    }
}
