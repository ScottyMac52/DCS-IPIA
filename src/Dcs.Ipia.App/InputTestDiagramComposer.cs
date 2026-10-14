using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;

namespace DcsIpia.Features.InputTest
{
    /// <summary>Renders kneeboard-quality authored labels while emphasizing only applicable live layers.</summary>
    public static class InputTestDiagramComposer
    {
        #region BEGIN Public
        /// <summary>Builds hardware artwork from a single canonical kneeboard projection and a live modifier chord.</summary>
        public static InputTestDiagramModel Compose(
            UiLayerDiagram diagram, EditorWorkspaceSnapshot workspace,
            IReadOnlyList<DraftAssignment> assignments, IReadOnlyDictionary<string, string> labels,
            IReadOnlyDictionary<AssignmentLayer, string>? activeModifiers = null)
        {
            ArgumentNullException.ThrowIfNull(diagram);
            var page = KneeboardDraftProjection.CreateCombined(workspace, diagram, assignments, labels);
            var png = RenderLive(diagram, page, activeModifiers ?? new Dictionary<AssignmentLayer, string>());
            var regions = diagram.Controls.Select(control =>
                new InputTestCallout(control.InputKey, control.Control, control.X, control.Y,
                    control.Width, control.Height)).ToArray();
            return new(png, diagram.Width, diagram.Height, regions, page.Legend, page);
        }

        /// <summary>Redraws the SVG for a changed modifier chord without recomputing its authored labels.</summary>
        public static byte[] RenderLive(UiLayerDiagram diagram, KneeboardComposite page,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers) =>
            CanonicalSvgRenderer.RenderComplete(CompositeKneeboardRenderer.BuildDeviceSvg(
                diagram, KneeboardLiveProjection.Focus(page, activeModifiers)), diagram.Width, diagram.Height);
        #endregion END Public
    }
}
