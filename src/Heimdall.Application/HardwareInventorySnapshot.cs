namespace Heimdall.Application
{
    /// <summary>Portable inventory format; module snapshots remain module-scoped.</summary>
    /// <param name="SchemaVersion">Inventory format, currently 1.</param>
    /// <param name="Definitions">Reusable pinned capability definitions.</param>
    /// <param name="Instances">User selections linked by exact physical identity.</param>
    /// <param name="Observations">Historical scan evidence; connection status must be refreshed after reopening.</param>
    public sealed record HardwareInventorySnapshot(int SchemaVersion, IReadOnlyList<HardwareDefinition> Definitions,
        IReadOnlyList<HardwareInventoryInstance> Instances, IReadOnlyList<ControllerObservation> Observations);
}
