namespace Heimdall.Application.Games
{
    /// <summary>Current workspace scope and common mutation gates supplied by a consumer without UI types.</summary>
    public readonly record struct WorkspaceAssignmentContext(string Scope, bool Open, bool ReadOnly, bool Busy, bool Testing)
    {
        #region BEGIN Public
        /// <summary>Gets the shared explanation for an unavailable editing state.</summary>
        public string BlockedReason => !Open ? "Open a configuration before editing assignments."
            : ReadOnly ? "This configuration is read-only."
            : Testing ? "Stop input testing before editing assignments."
            : Busy ? "Finish the current operation before editing assignments." : "";
        #endregion END Public
    }
}
