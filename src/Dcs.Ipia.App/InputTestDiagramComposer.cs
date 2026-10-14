using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;

namespace DcsIpia.Features.InputTest
{
    /// <summary>Creates the kneeboard-quality hardware image and transparent input hit regions.</summary>
    public static class InputTestDiagramComposer
    {
        #region BEGIN Public
        /// <summary>Uses the same combined labels, authored categories and legend as the device kneeboard.</summary>
        public static InputTestDiagramModel Compose(
            UiLayerDiagram diagram,
            EditorWorkspaceSnapshot workspace,
            IReadOnlyList<DraftAssignment> assignments,
            IReadOnlyDictionary<string, string> labels)
        {
            ArgumentNullException.ThrowIfNull(diagram);
            var page = KneeboardDraftProjection.CreateCombined(workspace, diagram, assignments, labels);
            var svg = CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page);
            var png = CanonicalSvgRenderer.RenderComplete(svg, diagram.Width, diagram.Height);
            var regions = diagram.Controls.Select(control =>
                new InputTestCallout(control.InputKey, control.Control, control.X, control.Y,
                    control.Width, control.Height, "", 0)).ToArray();
            return new(png, diagram.Width, diagram.Height, regions, page.Legend);
        }
        #endregion END Public
    }
}
