using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Infrastructure
{
    /// <summary>Restores shifted callout mappings in editor snapshots saved before they were imported.</summary>
    internal static class EditorWorkspaceUpgrade
    {
        #region BEGIN Public
        /// <summary>Upgrades a legacy snapshot using its stored SVG and catalogs, without reading source repositories.</summary>
        /// <param name="snapshot">The stored editor snapshot.</param>
        /// <returns>The snapshot with previously inaccessible shifted controls and their UI assignments.</returns>
        public static EditorWorkspaceSnapshot RestoreShiftedControls(EditorWorkspaceSnapshot snapshot)
        {
            var diagrams = snapshot.Diagrams.Select(RestoreDiagram).ToArray();
            var assignments = snapshot.Assignments.ToDictionary(x => x.Slot.Key, x => x, StringComparer.Ordinal);
            var uiCommands = snapshot.UiCommands.ToDictionary(x => x.DcsCommandId, x => x, StringComparer.Ordinal);
            foreach (var diagram in diagrams)
            {
                var original = snapshot.Diagrams.First(x => x.ProfileId == diagram.ProfileId);
                var newControls = diagram.Controls
                                      .Where(x => x.Control.EndsWith("-shifted", StringComparison.Ordinal) &&
                                                  original.Controls.All(y => y.Control != x.Control))
                                      .ToDictionary(x => x.Control, x => x, StringComparer.Ordinal);
                foreach (var callout in diagram.Callouts)
                {
                    if (!newControls.TryGetValue(callout.Control, out var control) ||
                        !uiCommands.TryGetValue(callout.CommandId, out var command) || command.IsAxis != control.IsAxis)
                        continue;
                    var chord = callout.Modifier.Replace(" + ", "+", StringComparison.Ordinal);
                    if (chord == "Base")
                        chord = "";
                    var slot = new DraftAssignmentSlot(
                        snapshot.Configuration.Module.Id, diagram.ProfileId, AssignmentLayer.UiLayer,
                        control.IsAxis ? BindingSection.Axis : BindingSection.Key,
                        DcsInputKey.Resolve(snapshot.Configuration, diagram.ProfileId,
                                            control.IsAxis ? BindingSection.Axis : BindingSection.Key,
                                            control.InputKey),
                        chord);
                    assignments.TryAdd(
                        slot.Key, new DraftAssignment(slot, new DraftCommand(command.DcsCommandId, command.Name,
                                                                             command.IsAxis, AssignmentLayer.UiLayer)));
                }
            }
            return snapshot with { Diagrams = diagrams, Assignments = assignments.Values.ToArray() };
        }
        /// <summary>Recovers imported assignments lost when canonical UI modifiers differed from module modifiers.</summary>
        /// <param name="snapshot">The stored workspace and relational module configuration.</param>
        /// <returns>Existing edits plus previously missing scope assignments.</returns>
        public static EditorWorkspaceSnapshot RestoreMissingImportedAssignments(EditorWorkspaceSnapshot snapshot)
        {
            var configuration = snapshot.Configuration;
            var uiModifiers = snapshot.Diagrams.SelectMany(x => x.Callouts)
                                  .SelectMany(x => x.Modifier.Split('+', StringSplitOptions.TrimEntries |
                                                                             StringSplitOptions.RemoveEmptyEntries))
                                  .ToHashSet(StringComparer.Ordinal);
            if (!uiModifiers.Any(x => configuration.Modifiers.All(y => y.Name != x)))
                return snapshot;
            var assignments = snapshot.Assignments.ToDictionary(x => x.Slot.Key, x => x, StringComparer.Ordinal);
            var moduleCommands = snapshot.ModuleCommands.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);
            var uiCommands = snapshot.UiCommands.ToDictionary(x => x.DcsCommandId, x => x, StringComparer.Ordinal);
            if (!assignments.Values.Any(x => x.Slot.Layer == AssignmentLayer.Module))
            {
                foreach (var binding in configuration.Bindings.Where(x => x.Disposition != BindingDisposition.Removed))
                {
                    var control = snapshot.Diagrams.FirstOrDefault(x => x.ProfileId == binding.ProfileId)
                                      ?.Controls.FirstOrDefault(
                                          x => DcsInputKey.SamePhysicalInput(x.InputKey, binding.InputKey) &&
                                               x.IsAxis == (binding.Section == BindingSection.Axis));
                    if (control is null || !moduleCommands.TryGetValue(binding.CommandId, out var command))
                        continue;
                    var slot =
                        new DraftAssignmentSlot(configuration.Module.Id, binding.ProfileId, AssignmentLayer.Module,
                                                binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
                    assignments.TryAdd(
                        slot.Key, new DraftAssignment(slot, new DraftCommand(command.Id, command.Name, command.IsAxis,
                                                                             AssignmentLayer.Module)));
                }
            }
            if (!assignments.Values.Any(x => x.Slot.Layer == AssignmentLayer.UiLayer))
            {
                foreach (var diagram in snapshot.Diagrams)
                    foreach (var callout in diagram.Callouts)
                    {
                        var control = diagram.Controls.FirstOrDefault(x => x.Control == callout.Control);
                        if (control is null || !uiCommands.TryGetValue(callout.CommandId, out var command) ||
                            control.IsAxis != command.IsAxis)
                            continue;
                        var chord = callout.Modifier.Replace(" + ", "+", StringComparison.Ordinal);
                        var slot = new DraftAssignmentSlot(
                            configuration.Module.Id, diagram.ProfileId, AssignmentLayer.UiLayer,
                            control.IsAxis ? BindingSection.Axis : BindingSection.Key, control.InputKey, chord);
                        assignments.TryAdd(
                            slot.Key,
                            new DraftAssignment(slot, new DraftCommand(command.DcsCommandId, command.Name,
                                                                       command.IsAxis, AssignmentLayer.UiLayer)));
                    }
            }
            return snapshot with { Assignments = assignments.Values.ToArray() };
        }
        /// <summary>Restores Hornet POV bindings previously invisible because the hardware and DCS key spellings differed.</summary>
        /// <param name="snapshot">The persisted editor snapshot.</param>
        /// <returns>A snapshot retaining edits while mapping previously inaccessible POV slots.</returns>
        public static EditorWorkspaceSnapshot RestorePovAliases(EditorWorkspaceSnapshot snapshot)
        {
            var configuration = snapshot.Configuration;
            var assignments = snapshot.Assignments.ToDictionary(x => x.Slot.Key, x => x, StringComparer.Ordinal);
            var labels = new Dictionary<string, string>(snapshot.Labels, StringComparer.Ordinal);
            foreach (var assignment in snapshot.Assignments.Where(
                         x => x.Slot.InputKey.StartsWith("JOY_POV", StringComparison.Ordinal)))
            {
                var slot = assignment.Slot;
                var actual = DcsInputKey.Resolve(configuration, slot.ProfileId, slot.Section, slot.InputKey);
                if (actual == slot.InputKey ||
                    !configuration.Bindings.Any(x => x.ProfileId == slot.ProfileId && x.Section == slot.Section &&
                                                     x.InputKey == actual))
                    continue;
                var destination = slot with { InputKey = actual };
                assignments.Remove(slot.Key);
                assignments.TryAdd(destination.Key, assignment with { Slot = destination });
                if (labels.Remove(slot.Key, out var label))
                    labels.TryAdd(destination.Key, label);
            }
            var commands = snapshot.ModuleCommands.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);
            foreach (var binding in configuration.Bindings.Where(
                         x => x.Disposition != BindingDisposition.Removed &&
                              x.InputKey.StartsWith("JOY_BTN_POV", StringComparison.Ordinal)))
            {
                var mapped = snapshot.Diagrams.FirstOrDefault(x => x.ProfileId == binding.ProfileId)
                                 ?.Controls.Any(x => x.InputKey.StartsWith("JOY_POV", StringComparison.Ordinal) &&
                                                     DcsInputKey.SamePhysicalInput(x.InputKey, binding.InputKey) &&
                                                     x.IsAxis == (binding.Section == BindingSection.Axis)) == true;
                if (!mapped || !commands.TryGetValue(binding.CommandId, out var command))
                    continue;
                var slot =
                    new DraftAssignmentSlot(configuration.Module.Id, binding.ProfileId, AssignmentLayer.Module,
                                            binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
                assignments.TryAdd(slot.Key,
                                   new DraftAssignment(slot, new DraftCommand(command.Id, command.Name, command.IsAxis,
                                                                              AssignmentLayer.Module)));
            }
            return snapshot with { Assignments = assignments.Values.ToArray(), Labels = labels };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UiLayerDiagram RestoreDiagram(UiLayerDiagram diagram)
        {
            if (string.IsNullOrWhiteSpace(diagram.Svg) || diagram.Controls.Count == 0)
                return diagram;
            using var reader = XmlReader.Create(new StringReader(diagram.Svg), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000
            });
            var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
            if (root is null)
                return diagram;
            var controls = diagram.Controls.ToList();
            var mapped = controls.ToDictionary(x => x.Control, x => x, StringComparer.Ordinal);
            foreach (var comment in root.Nodes().OfType<XComment>())
            {
                var marker = comment.Value.Trim();
                if (!marker.StartsWith("box:", StringComparison.Ordinal) ||
                    !marker.EndsWith("-shifted", StringComparison.Ordinal))
                    continue;
                var id = marker["box:".Length..].Trim();
                if (mapped.ContainsKey(id) || !mapped.TryGetValue(id[..^ "-shifted".Length], out var baseControl))
                    continue;
                XNode? next = comment.NextNode;
                while (next is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
                    next = next.NextNode;
                if (next is not XElement box || box.Name.LocalName != "rect")
                    continue;
                var label =
                    box.ElementsAfterSelf()
                        .FirstOrDefault(x => x.Name.LocalName == "text" && x.Attribute("id")?.Value == "lbl-" + id)
                        ?.Value.Trim() ??
                    baseControl.Label;
                var control =
                    new HardwareGraphicControl(id, label, baseControl.InputKey, Number(box, "x"), Number(box, "y"),
                                               Number(box, "width"), Number(box, "height"), baseControl.IsAxis);
                controls.Add(control);
                mapped.Add(id, control);
            }
            return diagram with { Controls = controls };
        }
        private static double Number(XElement box, string name) =>
            double.TryParse(box.Attribute(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out var value) &&
                    double.IsFinite(value) && value >= 0
                ? value
                : throw new InvalidDataException("Invalid stored SVG box coordinate: " + name);
        #endregion END private helpers
    }
}
