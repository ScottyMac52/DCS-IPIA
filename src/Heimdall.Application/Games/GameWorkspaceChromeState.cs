namespace Heimdall.Application.Games
{
    /// <summary>Shared, UI-agnostic profile chrome state for all saved game workspaces.
    /// The UI may supply an edited name before the session has staged it.</summary>
    public sealed record GameWorkspaceChromeState(bool HasProfile, bool Pending, bool CanSave,
        bool CanDiscard, bool CanExport, string Status)
    {
        /// <summary>Derives action availability without mutating a session or its native profile.</summary>
        public static GameWorkspaceChromeState For(GameProfileSession? session, string? revision,
            string? editedName, bool busy = false)
        {
            if (session is null) return new(false, false, false, false, false, "No profile");
            var pending = revision is null || session.HasChanges ||
                !string.Equals(editedName, session.Snapshot.Name, StringComparison.Ordinal);
            return new(true, pending, pending && !busy, pending && !busy,
                !pending && !busy && session.AccessMessage.Length == 0,
                pending ? "Unsaved changes" : "Saved");
        }
    }

    /// <summary>Decision requested by the owning UI's confirmation prompt.</summary>
    public enum GameWorkspaceLeaveDecision { Save, Discard, Cancel }

    /// <summary>Coordinates guarded profile/window transitions without knowing WPF, storage
    /// or a simulator's native format. The caller owns prompting and stopping live input.</summary>
    public static class GameWorkspaceLeaveGuard
    {
        /// <summary>Does not invoke the prompt for a clean session. A failed save or canceled
        /// prompt preserves the draft; discard executes only after explicit consent.</summary>
        public static bool Confirm(bool pending, Func<GameWorkspaceLeaveDecision> choose,
            Func<bool> save, Action discard)
        {
            ArgumentNullException.ThrowIfNull(choose);
            ArgumentNullException.ThrowIfNull(save);
            ArgumentNullException.ThrowIfNull(discard);
            if (!pending) return true;
            return choose() switch
            {
                GameWorkspaceLeaveDecision.Save => save(),
                GameWorkspaceLeaveDecision.Discard => Discard(),
                _ => false
            };
            bool Discard() { discard(); return true; }
        }
    }
}
