using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Owns the simulator-native feature surface composed by the shared workspace host.</summary>
    public partial class SquadronsAssignmentsView : UserControl
    {
        /// <summary>Creates the themed feature without owning session or native editing rules.</summary>
        public SquadronsAssignmentsView()
        {
            InitializeComponent();
            Artwork.Feedback.Text = "Click a control to select it, then Apply assignment; or drag a command onto a control.";
        }
        internal Slider DrawingZoom => Artwork.Zoom;
        internal TextBlock DrawingDropFeedback => Artwork.Feedback;
        internal TextBlock DrawingHint => Artwork.Hint;
        internal TextBox InputActivity => Artwork.Activity;
        internal ScrollViewer DrawingScroll => Artwork.Viewport;
        internal Canvas DrawingCanvas => Artwork.Surface;
    }
}

