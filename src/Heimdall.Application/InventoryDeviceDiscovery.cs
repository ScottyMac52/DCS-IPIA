using Heimdall.Application.Interfaces;

namespace Heimdall.Application
{
    /// <summary>Projects the shared adopted inventory onto current native connections for every game.</summary>
    public sealed class InventoryDeviceDiscovery(HardwareInventoryManager inventory, IConnectedDeviceDiscovery native)
        : IConnectedDeviceDiscovery, IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets the one profile-owned device inventory.</summary>
        public HardwareInventoryManager Inventory => inventory;
        /// <inheritdoc/>
        public IReadOnlyList<DetectedDcsDevice> Discover()
        {
            var adopted = inventory.Current.Instances.Where(x => x.Selected).ToDictionary(x => x.PhysicalGuid);
            return native.Discover().Where(x => x.Connected && adopted.ContainsKey(x.PhysicalGuid))
                .Select(x => x with { DisplayLabel = adopted[x.PhysicalGuid].Label }).ToArray();
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default)
        {
            var saved = await inventory.ReadSnapshotAsync(ct).ConfigureAwait(false);
            if (native is not IControllerCapabilityDiscovery capabilities) return [];
            var observed = await capabilities.DiscoverAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var adopted = saved.Instances.Where(x => x.Selected).ToDictionary(x => x.PhysicalGuid);
            return observed.Where(x => x.Device.Connected && adopted.TryGetValue(x.Device.PhysicalGuid, out var unit) &&
                x.Error is null && x.Capabilities is not null &&
                ControllerCapabilityMapper.Fingerprint(x.Capabilities) == unit.DefinitionId).ToArray();
        }
        #endregion END Public
    }
}
