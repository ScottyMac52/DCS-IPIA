using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Owns the simulator-native feature surface composed by the shared workspace host.</summary>
    public partial class SquadronsAssignmentsView : UserControl
    {
        /// <summary>Creates the themed feature without owning session or native editing rules.</summary>
        public SquadronsAssignmentsView() => InitializeComponent();
    }
}
