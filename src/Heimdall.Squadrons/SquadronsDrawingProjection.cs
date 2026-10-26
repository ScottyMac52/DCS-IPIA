using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Maps captured Squadrons joystick records to the shared drawing's canonical hardware control keys.
    /// Presentation-only: does not infer native slot identity or modify native assignments.</summary>
    public static class SquadronsDrawingProjection
    {
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
                return (IReadOnlyList<KneeboardLabelVariant>)assigned.Where(row =>
                        string.Equals(row.Native.Input!.PhysicalKey, key, StringComparison.Ordinal))
                    .Select(row => new KneeboardLabelVariant(
                        row.CommandName + (row.Native.Input!.Type == CommandType.Axis
                            ? " (" + (row.Native.Input.Direction > 0 ? "+" : "-") + ")" : ""),
                        KneeboardStateColors.Base, AssignmentLayer.Module, ""))
                    .DistinctBy(label => label.Text, StringComparer.Ordinal).ToArray();
            }, StringComparer.Ordinal);
            return new KneeboardComposite(title, "Star Wars: Squadrons · " + section,
                labels, new Dictionary<string, string>(StringComparer.Ordinal), []);
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
    }
}
