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
            // Controller IDs are domain references, not interchangeable numeric indices.
            // "all" means all input sources; system.0/1/2 are native read-only input types.
            var selection = ParseSource(controllerId);
            var query = (search ?? "").Trim();
            return commands.Where(command =>
            {
                if (category != "All" && command.Category != category) return false;
                if (!command.Metadata.TryGetValue("squadrons.concept", out var concept)) return false;
                var native = rows[concept].ToArray();
                if (!MatchesInputType(native, inputType)) return false;
                if (!MatchesAssignment(native, selection, assignmentStatus)) return false;
                return command.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || concept.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (command.Metadata.TryGetValue("squadrons.native-display-name", out var nativeName)
                        && nativeName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    || command.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
            }).OrderBy(x => x.Category, StringComparer.Ordinal)
                .ThenBy(x => x.DisplayName, StringComparer.Ordinal).ToArray();
        }
        // Unbound commands retain unknown type; their names never imply an axis or button.
        private static bool MatchesInputType(IReadOnlyList<SquadronsBindingRow> native, string inputType)
        {
            var joystick = native.Where(x => x.Type == 3 && x.Input is not null).ToArray();
            return inputType switch
            {
                "Axes" => joystick.Any(x => x.Input!.Type == CommandType.Axis),
                "Buttons and hats" => joystick.Any(x => x.Input!.Type is CommandType.Button or CommandType.Hat),
                "Unknown / unbound" => joystick.Length == 0,
                _ => true
            };
        }
        private static bool MatchesAssignment(SquadronsBindingRow[] native, Source selection, string status)
        {
            var onController = native.Any(x => selection.Kind switch
            {
                SourceKind.All => true,
                SourceKind.Joystick => x.Type == 3 && x.DeviceId == selection.Index,
                SourceKind.System => x.Type == selection.Index,
                _ => false
            });
            return status switch
            {
                "Assigned to selected controller" => onController,
                "Not assigned to selected controller" => !onController,
                "Assigned anywhere" => native.Length > 0,
                "Unassigned everywhere" => native.Length == 0,
                _ => true
            };
        }
        private enum SourceKind { All, Joystick, System }
        private readonly record struct Source(SourceKind Kind, int Index);

        /// <summary>Rejects malformed device references instead of slicing arbitrary strings.</summary>
        private static Source ParseSource(string? id)
        {
            if (id is null or "all") return new(SourceKind.All, -1);
            const string joystick = "squadrons.joystick.";
            if (id.StartsWith(joystick, StringComparison.Ordinal) &&
                int.TryParse(id[joystick.Length..], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var slot) && slot is >= 1 and <= 10)
                return new(SourceKind.Joystick, slot - 1);
            if (id is "system.0" or "system.1" or "system.2")
                return new(SourceKind.System, id[^1] - '0');
            throw new ArgumentException("Unsupported Squadrons assignment source.", nameof(id));
        }
    }
}
