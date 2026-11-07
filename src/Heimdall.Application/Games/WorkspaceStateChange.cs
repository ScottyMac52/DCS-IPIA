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
        /// <summary>Returns whether a distinct notification leaves native content and mounted identity unchanged.
        /// Pass a native-data snapshot, excluding pending name/selection overlays. Gates and name overlays refresh
        /// availability; native target-selection handlers explicitly load the chosen record.</summary>
        public static bool IsPresentationOnly(WorkspaceState? previous, string? previousSnapshot, WorkspaceState current, string currentSnapshot)
        {
            ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(currentSnapshot);
            if (previous is null || previousSnapshot is null || !string.Equals(previousSnapshot, currentSnapshot, StringComparison.Ordinal)) return false;
            return previous != current && previous.Identity == current.Identity && previous.Open == current.Open;
        }
        #endregion END Public
    }
}
