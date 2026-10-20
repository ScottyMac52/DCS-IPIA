namespace Heimdall.Application
{
    /// <summary>Review row combining saved selection with current scan evidence.</summary>
    /// <param name="PhysicalGuid">Exact physical unit.</param>
    /// <param name="Saved">Previously adopted unit, including disconnected selections.</param>
    /// <param name="Observed">Current scan evidence, absent until rescan or when disconnected.</param>
    /// <param name="Changed">Whether capabilities differ from the pinned saved definition.</param>
    public sealed record HardwareInventoryCandidate(Guid PhysicalGuid, HardwareInventoryInstance? Saved,
        ControllerObservation? Observed, bool Changed);
}
