namespace Heimdall.Application.Games
{
    /// <summary>Classifies context notifications so selection transitions do not reload native editors.</summary>
    public static class WorkspaceStateChange
    {
        #region BEGIN Public
        /// <summary>Returns whether only selection (and its version) changed while native content and all other state stayed identical.</summary>
        public static bool IsSelectionOnly(WorkspaceState? previous, string? previousSnapshot, WorkspaceState current, string currentSnapshot)
        {
            ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(currentSnapshot);
            if (previous is null || previousSnapshot is null || !string.Equals(previousSnapshot, currentSnapshot, StringComparison.Ordinal)) return false;
            return previous.Selection != current.Selection && previous with { Version = 0, Selection = current.Selection } == current with { Version = 0 };
        }
        /// <summary>Returns whether a notification changed only selection or input activity (and the capabilities that follow it).
        /// Native editors must keep pending drafts across these; availability is refreshed separately by the window.</summary>
        public static bool IsPresentationOnly(WorkspaceState? previous, string? previousSnapshot, WorkspaceState current, string currentSnapshot)
        {
            ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(currentSnapshot);
            if (previous is null || previousSnapshot is null || !string.Equals(previousSnapshot, currentSnapshot, StringComparison.Ordinal)) return false;
            var normalized = previous with { Version = 0, Selection = current.Selection, Activity = current.Activity, Capabilities = current.Capabilities };
            return previous != current && normalized == current with { Version = 0 };
        }
        #endregion END Public
    }
}
