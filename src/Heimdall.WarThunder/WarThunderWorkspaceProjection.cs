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
                    inputs.Count == 0 ? Assigned(binding) ? "System / other native fields" : "Unassigned" : string.Join(" + ", inputs), details);
            }).ToArray();
        }
        /// <summary>Filters captured command metadata and native ownership without physical aliases.</summary>
        public static IReadOnlyList<SimCommand> Filter(IReadOnlyList<SimCommand> catalog, IReadOnlyList<WarThunderWorkspaceRow> rows,
            string? deviceId, string search, string? category, CommandType? type, GameCommandAssignmentStatus status)
        {
            var query = new GameCommandQuery(Category: category, InputType: type, AssignmentStatus: status);
            return CommandEntries(catalog, rows).Where(entry => MatchesCommand(entry, query, search, deviceId)).Select(entry => entry.Command).ToArray();
        }
        /// <summary>Indexes native assignment presence and joystick ownership once, retaining system-only assignments.</summary>
        public static IReadOnlyList<GameCommandEntry> CommandEntries(IReadOnlyList<SimCommand> commands, IReadOnlyList<WarThunderWorkspaceRow> rows)
        {
            var owners = rows.ToLookup(x => x.Command.Id, StringComparer.Ordinal);
            return commands.Select(command => new GameCommandEntry(command, "", command.Category, [command.Type],
                owners[command.Id].SelectMany(row => row.DeviceIds).Distinct(StringComparer.Ordinal).ToArray())
                { HasAssignments = owners[command.Id].Any(row => Assigned(row.Binding)) }).ToArray();
        }
        /// <summary>Preserves native name/ID search and shared ownership/type/category policy for one indexed command.</summary>
        public static bool MatchesCommand(GameCommandEntry entry, GameCommandQuery query, string search, string? deviceId) =>
            (entry.Command.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || entry.Command.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
            && GameCommandBrowser.Matches(entry, query, deviceId);
        /// <summary>Resolves a captured input for native editing without exposing offset arithmetic to desktop controls.</summary>
        public static WarThunderNativeInputTarget? InputTarget(ProfileDocument profile, WarThunderBlkScalar scalar, CommandType type)
        {
            if (scalar.Type != 'i' || type is not (CommandType.Axis or CommandType.Button)
                || FieldName(scalar) != (type == CommandType.Axis ? "axisId" : "joyButton")
                || !int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var global)) return null;
            var owners = WarThunderProfileBackend.ResolveNativeInput(profile, type, global);
            return owners.Count == 1 ? new(owners[0].Id, global - (type == CommandType.Axis ? owners[0].AxesOffset : owners[0].ButtonsOffset)) : null;
        }
        /// <summary>Reads the captured inverse flag using native boolean spellings.</summary>
        public static bool IsInverted(WarThunderBinding binding) => binding.Fields.Any(scalar => scalar.Type == 'b' && FieldName(scalar) == "inverse" && scalar.Value is "yes" or "1");
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
