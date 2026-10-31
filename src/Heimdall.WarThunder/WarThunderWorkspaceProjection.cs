using System.Globalization;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Translates native records for desktop inspection; no Windows axis order is inferred.</summary>
    public static class WarThunderWorkspaceProjection
    {
        #region BEGIN Public
        /// <summary>Projects every captured command occurrence, retaining unknown fields.</summary>
        public static IReadOnlyList<WarThunderWorkspaceRow> Rows(ProfileDocument profile, IReadOnlyList<SimCommand> catalog)
        {
            var capturedDevices = WarThunderProfileBackend.ReadDevices(profile);
            return WarThunderProfileBackend.ReadBindings(profile).Select(binding =>
            {
                var command = catalog.Single(item => item.Metadata["war-thunder.section"] == binding.Section
                    && item.Metadata["war-thunder.command"] == binding.Command);
                var devices = new HashSet<string>(StringComparer.Ordinal);
                var inputs = new List<string>();
                foreach (var scalar in InputFields(binding))
                {
                    var type = FieldName(scalar) == "axisId" ? CommandType.Axis : CommandType.Button;
                    if (!int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) continue;
                    var owners = capturedDevices.Where(device => device.Contains(type, index)).ToArray();
                    if (owners.Length == 1) devices.Add(owners[0].Id);
                    inputs.Add(owners.Length == 1
                        ? owners[0].Name + " · " + owners[0].Id + " · native " + type + " "
                            + (index - (type == CommandType.Axis ? owners[0].AxesOffset : owners[0].ButtonsOffset)).ToString(CultureInfo.InvariantCulture)
                        : "Unresolved native " + type + " " + scalar.Value);
                }
                var details = string.Join("; ", binding.Fields.Select(scalar => FieldName(scalar) + ":" + scalar.Type + "=" + scalar.Value));
                return new WarThunderWorkspaceRow(binding, command, devices.ToArray(),
                    inputs.Count == 0 ? binding.Empty ? "Unassigned" : "System / other native fields" : string.Join(" + ", inputs), details);
            }).ToArray();
        }
        /// <summary>Filters captured command metadata and native ownership without physical aliases.</summary>
        public static IReadOnlyList<SimCommand> Filter(IReadOnlyList<SimCommand> catalog, IReadOnlyList<WarThunderWorkspaceRow> rows,
            string? deviceId, string search, string? category, CommandType? type, GameCommandAssignmentStatus status) =>
            catalog.Where(command => (category is null || command.Category == category) && (type is null || command.Type == type)
                && (command.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || command.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
                && (status switch
                {
                    GameCommandAssignmentStatus.AssignedToController => rows.Any(row => row.Command.Id == command.Id && row.DeviceIds.Contains(deviceId ?? "")),
                    GameCommandAssignmentStatus.NotAssignedToController => !rows.Any(row => row.Command.Id == command.Id && row.DeviceIds.Contains(deviceId ?? "")),
                    GameCommandAssignmentStatus.AssignedAnywhere => rows.Any(row => row.Command.Id == command.Id && Assigned(row.Binding)),
                    GameCommandAssignmentStatus.UnassignedEverywhere => !rows.Any(row => row.Command.Id == command.Id && Assigned(row.Binding)),
                    _ => true
                })).ToArray();
        /// <summary>Gets explicit native joystick input scalar occurrences.</summary>
        public static IReadOnlyList<WarThunderBlkScalar> InputFields(WarThunderBinding binding) => binding.Fields
            .Where(scalar => scalar.Type == 'i' && FieldName(scalar) is "joyButton" or "axisId").ToArray();
        /// <summary>Gets the captured scalar name without its enclosing path.</summary>
        public static string FieldName(WarThunderBlkScalar scalar) => scalar.Path[(scalar.Path.LastIndexOf('/') + 1)..];
        /// <summary>Builds a complete tab-separated native reference sheet.</summary>
        public static string ReferenceText(IReadOnlyList<WarThunderWorkspaceRow> rows) =>
            "Command\tRecord\tNative input (zero-based)\tFields\n" + string.Join("\n", rows.Select(row =>
                string.Join("\t", new[] { row.Name, row.RecordId, row.Inputs, row.Details }.Select(Clean))));
        #endregion END Public
        #region BEGIN private helpers
        private static bool Assigned(WarThunderBinding binding) => binding.Fields.Any(scalar => scalar.Type == 'i'
            && FieldName(scalar) is "joyButton" or "axisId" or "keyboardKey" or "mouseButton" or "mouseAxisId"
            && int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0);
        private static string Clean(string text) => text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        #endregion END private helpers
    }
}
