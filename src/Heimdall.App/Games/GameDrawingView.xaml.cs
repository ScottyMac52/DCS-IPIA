using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Owns the common zoom, direct canvas viewport, input fallback and drawing hints.</summary>
    public partial class GameDrawingView : UserControl
    {
        /// <summary>Creates the shared presentation without native assignment or acquisition logic.</summary>
        public GameDrawingView() => InitializeComponent();
    }
}
