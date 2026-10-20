namespace Heimdall.Application
{
    /// <summary>Read-only source comparison for a saved module; assignments and overlays remain isolated.</summary>
    /// <param name="ModuleId">Saved module identity.</param>
    /// <param name="ModuleName">Display name and DCS identifier.</param>
    /// <param name="Sources">Comparison for each proposed source change.</param>
    public sealed record SharedUiLayerModuleImpact(Guid ModuleId, string ModuleName,
        IReadOnlyList<SharedUiLayerModuleSourceImpact> Sources)
    {
        #region BEGIN Public
        /// <summary>Summarizes whether proposed source changes need review for this module.</summary>
        public string Status => Sources.Count == 0 ? "No default changes" :
            Sources.Any(x => x.Status != "Matches proposal") ? "Review required" : "Matches proposal";
        #endregion END Public
    }
}

