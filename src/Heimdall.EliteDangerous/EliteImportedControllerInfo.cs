using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Read-only import evidence, independent of confirmed physical associations.</summary>
    /// <param name="Name">Friendly candidate name or native reference.</param>
    /// <param name="Summary">Association status and imported binding counts.</param>
    /// <param name="Details">Native reference and exact candidate evidence.</param>
    public sealed record EliteImportedControllerInfo(string Name, string Summary, string Details)
    {
        #region BEGIN Public
        /// <summary>Describes a reference without modifying its identity, bindings or hardware link.</summary>
        /// <param name="device">Imported native reference.</param>
        /// <param name="pin">Explicitly confirmed association, if any.</param>
        /// <param name="inventory">Saved inventory, including offline observations.</param>
        /// <param name="live">Current adopted connected units.</param>
        /// <returns>Presentation-only labels and evidence.</returns>
        public static EliteImportedControllerInfo Create(LogicalDevice device, GameHardwarePin? pin,
            HardwareInventorySnapshot? inventory, IReadOnlyList<DetectedDcsDevice> live)
        {
            var identity = device.Identity ?? new PhysicalDeviceIdentity();
            var token = identity.SimSpecificIds.GetValueOrDefault("ed.device") ?? device.LogicalId;
            var reference = token + (identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : "");
            var counts = $"{device.Buttons.Count} button/hat bindings · {device.Axes.Count} axis bindings";
            if (token is "Keyboard" or "Mouse") return new(token, "System input · " + counts, reference);
            var units = inventory?.Instances.Where(x => x.Selected).ToArray() ?? [];
            var matcher = new EliteDeviceReferenceMatcher();
            var candidates = live.Concat(inventory?.Observations.Select(x => x.Device)
                    .Where(x => units.Any(unit => unit.PhysicalGuid == x.PhysicalGuid)) ?? [])
                .DistinctBy(x => x.PhysicalGuid).Where(x => matcher.Matches(identity, x)).ToArray();
            string Label(DetectedDcsDevice candidate) => units.FirstOrDefault(x => x.PhysicalGuid == candidate.PhysicalGuid)?.Label
                ?? candidate.DisplayLabel ?? candidate.Name;
            if (pin is not null)
            {
                var connected = live.Any(x => x.Connected && x.PhysicalGuid == pin.Device.PhysicalGuid);
                return new(Label(pin.Device), "Confirmed link · " + (connected ? "Connected" : "Offline / unavailable") + " · " + counts,
                    reference + "\nPhysical GUID: " + pin.Device.PhysicalGuid + "\nNative: " + pin.Device.Name);
            }
            var name = candidates.Length == 0 ? reference : string.Join(" / ", candidates.Select(Label).Distinct(StringComparer.Ordinal));
            var status = candidates.Length == 0 ? "Unidentified" : candidates.Length == 1 ? "Suggested product match" : $"{candidates.Length} matching units";
            var evidence = candidates.Select(candidate => Label(candidate) + " · " + candidate.PhysicalGuid + " · " +
                (live.Any(x => x.Connected && x.PhysicalGuid == candidate.PhysicalGuid) ? "Connected" : "Offline") +
                (units.FirstOrDefault(x => x.PhysicalGuid == candidate.PhysicalGuid)?.Role is { Length: > 0 } role ? " · " + role : ""));
            return new(name, status + " · Needs identification · " + counts,
                reference + "\nProduct matches do not confirm a physical unit. Use Identify Controller.\n" + string.Join("\n", evidence));
        }
        #endregion END Public
    }
}
