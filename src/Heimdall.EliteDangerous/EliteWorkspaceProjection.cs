using Heimdall.Application;
using System.Xml;
using System.Xml.Linq;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Reuses shared hardware geometry and rendering without converting a game preset into a DCS module.</summary>
    public static class EliteWorkspaceProjection
    {
        #region BEGIN Public
        /// <summary>Gets independent authored assignments, keeping both slots and each source device.</summary>
        public static IReadOnlyList<EliteAssignmentRow> Rows(ProfileDocument profile, ICommandCatalog catalog)
        {
            var commands = catalog.Commands.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var rows = new List<EliteAssignmentRow>();
            foreach (var device in profile.Devices)
            {
                rows.AddRange(device.Buttons.Select(x => new EliteAssignmentRow(device.LogicalId, x.CommandId,
                    commands[x.CommandId].DisplayName, commands[x.CommandId].Category, x.SimSpecific["ed.slot"].ToString()!,
                    x.SourceButton, x.Mode, x.Modifiers.Select(m => new Modifier { Source = m.Source, Value = m.Value }).ToArray())));
                rows.AddRange(device.Axes.Select(x => new EliteAssignmentRow(device.LogicalId, x.CommandId,
                    commands[x.CommandId].DisplayName, commands[x.CommandId].Category, "Binding", x.SourceAxis, BindingMode.Press,
                    [], true, x.Invert, x.Deadzone)));
            }
            return rows;
        }
        /// <summary>Builds labeled controller artwork and a printable reference sheet from the same projection.</summary>
        public static KneeboardComposite Diagram(string title, UiLayerDiagram diagram,
            IReadOnlyList<EliteAssignmentRow> rows, string section)
        {
            var labels = diagram.Controls.ToDictionary(control => control.Control, control =>
                (IReadOnlyList<KneeboardLabelVariant>)rows.Where(x => x.Input == EliteInputKeys.FromShared(control.InputKey)
                    && (section == "All" || x.Section == section)).Select(x => new KneeboardLabelVariant(
                        x.CommandName + (x.Slot == "Secondary" ? " [S]" : "") + (x.Mode == BindingMode.Hold ? " [Hold]" : ""),
                        KneeboardStateColors.Base, AssignmentLayer.Module, "")).ToArray(), StringComparer.Ordinal);
            return new(title, "Elite Dangerous · " + section, labels, Categories(diagram.Svg), []);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, string> Categories(string svg)
        {
            if (string.IsNullOrWhiteSpace(svg)) return new(StringComparer.Ordinal);
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
            return XDocument.Load(reader).Descendants().Where(x => x.Name.LocalName == "text" &&
                ((string?)x.Attribute("id"))?.StartsWith("meta-", StringComparison.Ordinal) == true)
                .ToDictionary(x => ((string)x.Attribute("id")!)[5..], x => x.Value, StringComparer.Ordinal);
        }
        #endregion END private helpers
    }
}
