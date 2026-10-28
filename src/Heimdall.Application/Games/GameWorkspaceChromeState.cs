namespace Heimdall.Application.Games
{
    /// <summary>Shared, UI-agnostic profile chrome state for all saved game workspaces.
    /// The UI may supply an edited name before the session has staged it.</summary>
    public sealed record GameWorkspaceChromeState(bool HasProfile, bool Pending, bool CanSave,
        bool CanDiscard, bool CanExport, string Status)
    {
        #region BEGIN Public
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
        #endregion END Public
    }
}
