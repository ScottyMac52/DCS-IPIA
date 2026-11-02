using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Owns the controller artwork and assignment table surface independently of workspace navigation.</summary>
    public partial class EliteAssignmentArtworkView : UserControl
    {
        #region BEGIN Public
        /// <summary>Creates the themed feature component.</summary>
        public EliteAssignmentArtworkView()
        {
            InitializeComponent();
            DrawingSurface.Feedback.Text = "Choose a command and input below, then Apply assignment. Start input test for raw controller feedback.";
        }
        internal Slider DiagramZoom => DrawingSurface.Zoom;
        internal TextBlock InputActivityHint => DrawingSurface.Feedback;
        internal TextBox InputActivity => DrawingSurface.Activity;
        internal TextBlock DiagramHint => DrawingSurface.Hint;
        internal ScrollViewer DiagramScroll => DrawingSurface.Viewport;
        internal Canvas DiagramCanvas => DrawingSurface.Surface;
        #endregion END Public
    }
}
