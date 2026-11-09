namespace Heimdall.Application.Interfaces
{
    /// <summary>Common browser lifecycle and identity-based selection; browsing never changes native assignments.</summary>
    public interface IGameAssignmentBrowser : IDisposable
    {
        #region BEGIN Public
        /// <summary>Gets deterministic content-build and publication counters.</summary>
        Heimdall.Application.Games.BrowserWorkCounts Metrics { get; }
        /// <summary>Occurs after a complete native projection or selection has been bound by the presentation edge.</summary>
        event EventHandler? ProjectionChanged;
        /// <summary>Refreshes cached native projections, retaining a valid preferred command.</summary>
        void Refresh(string? preferredCommandId = null);
        /// <summary>Updates trimmed search text through the common refresh sequence.</summary>
        void Search(string text);
        /// <summary>Suspends selection/notifications while a cached feature is unmounted; reactivation refreshes it.</summary>
        void SetActive(bool active);
        /// <summary>Selects a visible command by stable identity; an unknown identity clears unsafe targets.</summary>
        void SelectCommand(string? commandId);
        /// <summary>Selects a visible record by its native stable address.</summary>
        void SelectRecord(string? recordId);
        #endregion END Public
    }
}

