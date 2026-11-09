namespace Heimdall.Application.Games
{
    /// <summary>Deterministic browser work counters, independent of machine speed and UI timing noise.</summary>
    public sealed record BrowserWorkCounts(int ContentBuilds, int Publications);
}
