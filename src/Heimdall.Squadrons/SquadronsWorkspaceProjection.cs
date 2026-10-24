using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Provides display-only rows and filters independently of WPF and native editing.</summary>
    public static class SquadronsWorkspaceProjection
    {
        #region BEGIN Public
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
        /// <summary>Intersects game context, decoded input type, text and captured device without mutating native state.</summary>
        public static IReadOnlyList<SquadronsWorkspaceRow> Filter(IEnumerable<SquadronsWorkspaceRow> rows, string section, string type, string search, string deviceId)
        {
            var text = search.Trim();
            return rows.Where(x => (section == "All" || x.Section == section) && (deviceId == "all" || x.DeviceId == deviceId)
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
            _ => true
        };
        #endregion END private helpers
    }
}
