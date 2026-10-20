using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Offers compatible catalog candidates using native recognition plus exact enumerated control evidence.</summary>
    public static class BundledHardwareCapabilityMatcher
    {
        #region BEGIN Public
        /// <summary>Retains ambiguous candidates without selecting artwork or assuming an attached grip.</summary>
        /// <param name="sources">Pinned bundled hardware metadata.</param>
        /// <param name="observed">Current native identity and controls.</param>
        /// <returns>Compatible candidate IDs, or an empty set for unknown/incomplete evidence.</returns>
        public static IReadOnlyList<string> Candidates(IReadOnlyDictionary<string, string> sources, ControllerObservation observed)
        {
            if (observed.Capabilities is not { } capabilities || capabilities.Inputs.Count == 0 || capabilities.Unsupported.Count > 0 ||
                !sources.TryGetValue("config/ipia-hardware.json", out var hardware)) return [];
            var choices = JsonSerializer.Deserialize<WorkspaceHardware[]>(hardware) ?? [];
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (sources.TryGetValue("config/ipia-device-map.json", out var mapping))
            {
                var map = JsonSerializer.Deserialize<SharedDeviceMap>(mapping);
                var native = observed.Device.Name.Replace(" - ", " ", StringComparison.Ordinal);
                foreach (var rule in map?.Rules ?? [])
                    if (rule.Patterns.Any(x => native.Contains(x.Replace(" - ", " ", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)))
                        names.Add(rule.DeviceKey);
            }
            var keys = capabilities.DcsKeys.ToHashSet(StringComparer.Ordinal);
            return choices.Where(x => names.Contains(x.DeviceId) ||
                    (DcsPhysicalIdentity.FromFileName(x.DefaultFileName) is not null &&
                     DcsPhysicalIdentity.NameFromFileName(x.DefaultFileName).Equals(observed.Device.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(x => keys.SetEquals(x.Diagram.Controls.Select(c => c.InputKey)))
                .Select(x => x.DeviceId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
        #endregion END Public
    }
}
