using Heimdall.Application.Interfaces;

namespace Heimdall.Application
{
    /// <summary>Projects the shared adopted inventory onto current native connections for every game.</summary>
    public sealed class InventoryDeviceDiscovery(HardwareInventoryManager inventory, IConnectedDeviceDiscovery native)
        : IConnectedDeviceDiscovery, IControllerCapabilityDiscovery
    {
        private IReadOnlyList<DetectedDcsDevice> _observed = [];
        #region BEGIN Public
        /// <summary>Gets the one profile-owned device inventory.</summary>
        public HardwareInventoryManager Inventory => inventory;
        /// <summary>Projects the last explicit scan without native I/O; rendering and availability checks use this snapshot.</summary>
        public IReadOnlyList<DetectedDcsDevice> Current
        {
            get
            {
                var adopted = inventory.Current.Instances.Where(x => x.Selected).ToDictionary(x => x.PhysicalGuid);
                return Volatile.Read(ref _observed).Where(x => x.Connected && adopted.ContainsKey(x.PhysicalGuid))
                    .Select(x => x with { DisplayLabel = adopted[x.PhysicalGuid].Label }).ToArray();
            }
        }
        /// <inheritdoc/>
        public IReadOnlyList<DetectedDcsDevice> Discover()
        {
            try { Volatile.Write(ref _observed, native.Discover()); }
            catch { Volatile.Write(ref _observed, []); throw; }
            return Current;
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default)
        {
            var saved = await inventory.ReadSnapshotAsync(ct).ConfigureAwait(false);
            if (native is not IControllerCapabilityDiscovery capabilities) return [];
            var observed = await capabilities.DiscoverAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            Volatile.Write(ref _observed, observed.Select(x => x.Device).ToArray());
            var adopted = saved.Instances.Where(x => x.Selected).ToDictionary(x => x.PhysicalGuid);
            return observed.Where(x => x.Device.Connected && adopted.TryGetValue(x.Device.PhysicalGuid, out var unit) &&
                x.Error is null && x.Capabilities is not null &&
                ControllerCapabilityMapper.Fingerprint(x.Capabilities) == unit.DefinitionId).ToArray();
        }
        #endregion END Public
    }
}
