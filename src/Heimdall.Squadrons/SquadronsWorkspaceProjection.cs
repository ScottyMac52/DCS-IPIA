using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Provides display-only rows and filters independently of WPF and native editing.</summary>
    public static class SquadronsWorkspaceProjection
    {
        #region BEGIN Public
        /// <summary>Orders controller choices numerically without changing captured slot order or native identity.</summary>
        public static IReadOnlyList<LogicalDevice> OrderedDevices(ProfileDocument profile) => profile.Devices
            .OrderBy(x => int.Parse(x.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        /// <summary>Distinguishes raw captured records from supported physical inputs without guessing another controller or deleting evidence.</summary>
        public static IReadOnlyList<SquadronsWorkspaceRow> ReviewInputs(IEnumerable<SquadronsWorkspaceRow> rows, IReadOnlyDictionary<string, IReadOnlyCollection<string>> verified) =>
            rows.Select(row => row with { HardwareStatus = row.Native.Type != 3 ? "Preserved system input" : row.Native.Input is not { } input ? "Preserved native encoding" :
                !verified.TryGetValue(row.DeviceId, out var keys) ? "Hardware unverified" : keys.Contains(input.PhysicalKey, StringComparer.Ordinal) ? "Supported physical input (slot unconfirmed)" : "Input absent on linked device" }).ToArray();
        /// <summary>Lists every native record with explicit slot ownership, retaining duplicate names and unsupported records.</summary>
        public static IReadOnlyList<SquadronsWorkspaceRow> Rows(ProfileDocument profile)
        {
            var backend = new SquadronsProfileBackend();
            var commands = backend.GetCatalog(profile).Commands.ToDictionary(x => x.Metadata["squadrons.concept"], StringComparer.Ordinal);
            var devices = profile.Devices.ToDictionary(x => x.LogicalId, StringComparer.Ordinal);
            return backend.ReadBindings(profile).Select(row =>
            {
                var id = "squadrons.joystick." + (row.DeviceId + 1);
                var label = row.Type == 3 ? "Slot " + (row.DeviceId + 1) + " · " + (devices.GetValueOrDefault(id)?.Identity?.WindowsName ?? "Unresolved")
                    : row.Type switch { 0 => "Mouse", 1 => "Gamepad", 2 => "Keyboard", _ => "Unassigned / preserved" };
                var command = commands[row.Concept];
                var name = row.Concept.EndsWith(".ConceptThrottle", StringComparison.Ordinal) ? row.Fields["negate"] switch
                { 0 => "Throttle Increase", 1 => "Throttle Decrease", _ => command.DisplayName } : command.DisplayName;
                return new SquadronsWorkspaceRow(row, name, command.Category, row.Type == 3 ? id : "system." + row.Type, label);
            }).ToArray();
        }
        /// <summary>Intersects context/type/text/device filters; normal authoring can exclude raw unverified and incompatible records.</summary>
        public static IReadOnlyList<SquadronsWorkspaceRow> Filter(IEnumerable<SquadronsWorkspaceRow> rows, string section, string type, string search, string deviceId, bool includeUnverified = true)
        {
            var text = search.Trim();
            return rows.Where(x => (includeUnverified || x.HardwareStatus == "Supported physical input (slot unconfirmed)") && (section == "All" || x.Section == section) && (deviceId == "all" || x.DeviceId == deviceId)
                && (x.CommandName.Contains(text, StringComparison.OrdinalIgnoreCase) || x.InputLabel.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || x.DeviceLabel.Contains(text, StringComparison.OrdinalIgnoreCase)) && MatchesType(x, type)).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool MatchesType(SquadronsWorkspaceRow row, string type) => type switch
        {
            "Axes" => row.Native.Input?.Type == CommandType.Axis,
            "Buttons and hats" => row.Native.Input?.Type is CommandType.Button or CommandType.Hat,
            "Preserved inputs" => row.Native.Input is null,
            "Supported physical inputs" => row.HardwareStatus == "Supported physical input (slot unconfirmed)",
            _ => true
        };
        #endregion END private helpers
    }
}
