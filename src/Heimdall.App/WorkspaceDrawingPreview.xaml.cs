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
        private UiLayerDiagram? _diagram;
        private KneeboardComposite? _page;
        private double _opacity;
        private bool _dirty;

        #region BEGIN Public
        /// <summary>Creates an empty, accessible drawing preview.</summary>
        public WorkspaceDrawingPreview()
        {
            InitializeComponent();
            IsVisibleChanged += (_, _) => RenderCurrent();
        }

        /// <summary>Shows the current controller with its staged labels using the shared SVG renderer.</summary>
        internal void Show(UiLayerDiagram diagram, KneeboardComposite page, double opacity)
        {
            _diagram = diagram; _page = page; _opacity = opacity; _dirty = true;
            RenderCurrent();
        }

        /// <summary>Clears stale artwork when a controller or drawing is unavailable.</summary>
        internal void Clear(string message = "Select a controller to preview its drawing. Drawings are optional.")
        {
            _diagram = null; _page = null; _dirty = false;
            Drawing.Source = null; EmptyHint.Text = message; EmptyHint.Visibility = Visibility.Visible;
        }
        #endregion END Public

        #region BEGIN private helpers
        private void RenderCurrent()
        {
            if (!IsVisible || !_dirty || _diagram is null || _page is null) return;
            _dirty = false;
            if (_diagram.DrawingUnlinked || string.IsNullOrWhiteSpace(_diagram.Svg)) { Clear("No drawing available for this controller. Assignments remain available."); return; }
            try
            {
                var svg = PreviewArtwork.Prepare(CompositeKneeboardRenderer.BuildDeviceSvg(_diagram, _page), _opacity);
                var bytes = CanonicalSvgRenderer.RenderComplete(svg, _diagram.Width, _diagram.Height);
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream; image.EndInit(); image.Freeze();
                Drawing.Source = image; EmptyHint.Visibility = Visibility.Collapsed;
            }
            catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
            {
                Clear("The drawing could not be previewed. You can still assign native inputs or choose another drawing.");
            }
        }
        #endregion END private helpers
    }
}
