namespace Heimdall.Application
{
    /// <summary>Source evidence for reviewing a draft before saving or adopting shared defaults.</summary>
    /// <param name="BaselineRevision">Opened saved revision used for comparison.</param>
    /// <param name="Changes">Ordinal source changes from baseline to draft.</param>
    /// <param name="Modules">Saved modules ordered by DCS identifier and identity.</param>
    public sealed record SharedUiLayerImpactReport(int BaselineRevision,
        IReadOnlyList<SharedUiLayerSourceChange> Changes, IReadOnlyList<SharedUiLayerModuleImpact> Modules);
}

