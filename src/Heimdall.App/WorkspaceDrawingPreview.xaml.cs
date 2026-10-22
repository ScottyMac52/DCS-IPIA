using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Heimdall.Application;
using Heimdall.Rendering;

namespace Heimdall.App
{
    /// <summary>Displays canonical controller artwork without editing or coupling native game identities.</summary>
    public partial class WorkspaceDrawingPreview : UserControl
    {
        #region BEGIN Public
        /// <summary>Creates an empty, accessible drawing preview.</summary>
        public WorkspaceDrawingPreview() => InitializeComponent();

        /// <summary>Shows the current controller with its staged labels using the shared SVG renderer.</summary>
        internal void Show(UiLayerDiagram diagram, KneeboardComposite page, double opacity)
        {
            if (string.IsNullOrWhiteSpace(diagram.Svg)) { Clear("No drawing available for this controller. Assignments remain available."); return; }
            var svg = PreviewArtwork.Prepare(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page), opacity);
            var bytes = CanonicalSvgRenderer.RenderComplete(svg, diagram.Width, diagram.Height);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit(); image.Freeze();
            Drawing.Source = image; EmptyHint.Visibility = Visibility.Collapsed;
        }

        /// <summary>Clears stale artwork when a controller or drawing is unavailable.</summary>
        internal void Clear(string message = "Select a controller to preview its drawing. Drawings are optional.")
        {
            Drawing.Source = null; EmptyHint.Text = message; EmptyHint.Visibility = Visibility.Visible;
        }
        #endregion END Public
    }
}
