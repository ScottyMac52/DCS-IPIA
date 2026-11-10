namespace Heimdall.Application.Interfaces
{
    /// <summary>Narrow native captured-record selection extension used by Squadrons editing.</summary>
    public interface ISquadronsAssignmentBrowser<TRow> : IGameAssignmentBrowser
    {
        #region BEGIN Public
        /// <summary>Refreshes the native captured-record query.</summary>
        void RefreshRows();
        /// <summary>Selects the native captured record through the common browser.</summary>
        void SelectRecord(TRow record);
        /// <summary>Selects a native concept, clearing display filters when necessary.</summary>
        void SelectCommandConcept(string concept);
        #endregion END Public
    }
}
