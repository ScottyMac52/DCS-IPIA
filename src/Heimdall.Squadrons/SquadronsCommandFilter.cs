using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Pure command-catalog filtering, independent of WPF and the captured-binding table.</summary>
    public static class SquadronsCommandFilter
    {
        /// <summary>Filters by native context, inferred type, captured assignment ownership and text.</summary>
        public static IReadOnlyList<SimCommand> Apply(IEnumerable<SimCommand> commands,
            IEnumerable<SquadronsBindingRow> records, string category, string inputType,
            string assignmentStatus, string search, string? controllerId)
        {
            ArgumentNullException.ThrowIfNull(commands);
            ArgumentNullException.ThrowIfNull(records);
            var rows = records.ToLookup(x => x.Concept, StringComparer.Ordinal);
            var slot = controllerId is null ? (int?)null
                : int.Parse(controllerId["squadrons.joystick.".Length..],
                    System.Globalization.CultureInfo.InvariantCulture) - 1;
            var query = (search ?? "").Trim();
            return commands.Where(command =>
            {
                if (category != "All" && command.Category != category) return false;
                if (!command.Metadata.TryGetValue("squadrons.concept", out var concept)) return false;
                var native = rows[concept].ToArray();
                // An unbound command has unknown type: never classify it as an axis/button by guessing.
                var joystick = native.Where(x => x.Type == 3 && x.Input is not null).ToArray();
                if (inputType == "Axes" && !joystick.Any(x => x.Input!.Type == CommandType.Axis)) return false;
                if (inputType == "Buttons and hats" && !joystick.Any(x => x.Input!.Type is CommandType.Button or CommandType.Hat)) return false;
                if (inputType == "Unknown / unbound" && joystick.Length > 0) return false;
                var any = native.Length > 0;
                var onController = native.Any(x => x.Type == 3 && (slot is null || x.DeviceId == slot));
                if (assignmentStatus == "Assigned to selected controller" && !onController) return false;
                if (assignmentStatus == "Not assigned to selected controller" && onController) return false;
                if (assignmentStatus == "Assigned anywhere" && !any) return false;
                if (assignmentStatus == "Unassigned everywhere" && any) return false;
                return command.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || concept.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || command.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
            }).OrderBy(x => x.Category, StringComparer.Ordinal)
                .ThenBy(x => x.DisplayName, StringComparer.Ordinal).ToArray();
        }
    }
}
