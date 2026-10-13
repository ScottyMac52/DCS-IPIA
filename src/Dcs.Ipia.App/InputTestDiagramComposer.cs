using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Rendering;

namespace DcsIpia.Features.InputTest
{

    /// <summary>Composes a device background and assignment slices for live input feedback.</summary>
    public static class InputTestDiagramComposer
    {
        #region BEGIN Public
        /// <summary>Creates the live input diagram from the supplied workspace, assignments and personal labels.</summary>
        public static InputTestDiagramModel Compose(
            UiLayerDiagram diagram,
            EditorWorkspaceSnapshot workspace,
            IReadOnlyList<DraftAssignment> assignments,
            IReadOnlyDictionary<string, string> labels)
        {
            var png = CanonicalSvgRenderer.Render(diagram.Svg, diagram.Width, diagram.Height);

            var moduleModifiers = WorkspaceStructureEditor.Describe(workspace)
                .Modifiers.Where(x => x.Layer == AssignmentLayer.Module)
                .Select(x => x.Name)
                .Prepend("")
                .Distinct()
                .ToArray();

            var uiModifiers = WorkspaceStructureEditor.Describe(workspace)
                .Modifiers.Where(x => x.Layer == AssignmentLayer.UiLayer)
                .Select(x => x.Name)
                .Prepend("")
                .Distinct()
                .ToArray();

            var layers = moduleModifiers.Select(m => (Layer: AssignmentLayer.Module, Chord: m))
                .Concat(uiModifiers.Select(m => (Layer: AssignmentLayer.UiLayer, Chord: m)))
                .ToArray();

            var callouts = new List<InputTestCallout>();
            foreach (var control in diagram.Controls)
            {
                var slices = new List<(string Key, string Caption, int ColorIndex)>();
                for (var i = 0; i < layers.Length; i++)
                {
                    var (layer, chord) = layers[i];
                    var slot = KneeboardDraftProjection.SlotForControl(workspace, diagram, control, layer, chord);
                    if (slot is null) continue;
                    var assignment = assignments.FirstOrDefault(x =>
                        DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot) &&
                        x.Slot.Layer == layer);
                    if (assignment is null && (chord.Length > 0 || layer == AssignmentLayer.UiLayer)) continue;
                    var caption = assignment is null
                        ? ""
                        : labels.GetValueOrDefault(slot.Key) ?? assignment.Command.Name;
                    slices.Add(($"{layer}|{slot.ModifierChord}", caption, i));
                }
                if (slices.Count == 0) continue;

                var sliceHeight = control.Height / slices.Count;
                for (var i = 0; i < slices.Count; i++)
                {
                    var (key, caption, colorIndex) = slices[i];
                    callouts.Add(new(
                        control.InputKey,
                        key,
                        control.X,
                        control.Y + i * sliceHeight,
                        control.Width,
                        Math.Max(14, sliceHeight),
                        caption,
                        colorIndex));
                }
            }

            return new(png, diagram.Width, diagram.Height, callouts);
        }
        #endregion END Public
    }
}
