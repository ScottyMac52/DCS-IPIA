using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Maps captured Squadrons joystick records to the shared drawing's canonical hardware control keys.
    /// Presentation-only: does not infer native slot identity or modify native assignments.</summary>
    public static class SquadronsDrawingProjection
    {
        #region BEGIN Public
        /// <summary>Builds callout labels for the selected captured slot, including multiple commands per physical control.</summary>
        public static KneeboardComposite Diagram(string title, UiLayerDiagram diagram,
            IEnumerable<SquadronsWorkspaceRow> rows, string logicalDeviceId, string section)
        {
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(rows);
            var assigned = rows.Where(row => row.DeviceId == logicalDeviceId && row.Native.Type == 3
                && row.Native.Input is not null && (section == "All" || row.Section == section)).ToArray();
            var labels = diagram.Controls.ToDictionary(control => control.Control, control =>
            {
                var key = SquadronsInputCodec.PhysicalKey(control.InputKey);
                var inputs = assigned.Where(row =>
                    string.Equals(row.Native.Input!.PhysicalKey, key, StringComparison.Ordinal)).ToArray();
                return (IReadOnlyList<KneeboardLabelVariant>)Names(inputs)
                    .Select(name => new KneeboardLabelVariant(
                        name, KneeboardStateColors.Base, AssignmentLayer.Module, "")).ToArray();
            }, StringComparer.Ordinal);
            return new KneeboardComposite(title, "Star Wars: Squadrons · " + section,
                labels, new Dictionary<string, string>(StringComparer.Ordinal), []);
        }

        /// <summary>Explains all commands using one physical callout. Opposite
        /// native directions of the same command count as one binding; two distinct
        /// concepts on the same axis are an explicit collision, not duplicate labels.</summary>
        public static string AssignmentSummary(HardwareGraphicControl control,
            IEnumerable<SquadronsWorkspaceRow> rows, string logicalDeviceId, string section)
        {
            var matches = ForControl(control, rows, logicalDeviceId, section);
            if (matches.Count == 0) return "No captured assignments";
            var concepts = matches.GroupBy(x => x.Native.Concept, StringComparer.Ordinal).ToArray();
            return (control.IsAxis && concepts.Length > 1
                ? "⚠ Shared axis: multiple commands assigned\n" : "")
                + string.Join("\n", Names(matches));
        }

        /// <summary>Returns assignments underlying a drawing control for tooltips and selection.</summary>
        public static IReadOnlyList<SquadronsWorkspaceRow> ForControl(HardwareGraphicControl control,
            IEnumerable<SquadronsWorkspaceRow> rows, string logicalDeviceId, string section)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(rows);
            var key = SquadronsInputCodec.PhysicalKey(control.InputKey);
            return rows.Where(row => row.Native.Type == 3 && row.DeviceId == logicalDeviceId &&
                row.Native.Input is not null && row.Native.Input.PhysicalKey == key &&
                (section == "All" || row.Section == section)).ToArray();
        }

        #endregion END Public
        #region BEGIN private helpers
        private static IEnumerable<string> Names(IEnumerable<SquadronsWorkspaceRow> rows) => rows
            .GroupBy(row => row.Native.Concept, StringComparer.Ordinal)
            .SelectMany(group =>
            {
                if (!group.All(row => row.Native.Input?.Type == CommandType.Axis))
                    return group.Select(row => row.CommandName);
                var directions = group.Select(row => row.Native.Input!.Direction).Distinct().ToArray();
                var suffix = directions.Length == 1
                    ? directions[0] > 0 ? " (+ only)" : " (- only)" : "";
                return new[] { group.First().ConceptName + suffix };
            }).Distinct(StringComparer.Ordinal);
        #endregion END private helpers
    }
}
