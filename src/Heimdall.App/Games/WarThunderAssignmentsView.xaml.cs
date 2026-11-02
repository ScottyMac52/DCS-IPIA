using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Composes the shared command browser with occurrence-based native editing and physical feedback.</summary>
    public partial class WarThunderAssignmentsView : UserControl
    {
        #region BEGIN Public
        public WarThunderAssignmentsView()
        {
            InitializeComponent();
            Artwork.Feedback.Text = "Choose a command and input below, then Apply assignment. Native input indices are zero-based.";
        }
        internal Slider Zoom => Artwork.Zoom;
        internal Canvas DrawingCanvas => Artwork.Surface;
        internal ScrollViewer DrawingScroll => Artwork.Viewport;
        internal TextBox Activity => Artwork.Activity;
        #endregion END Public
    }
}
