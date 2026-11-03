using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Provides the common controller-setup disclosure while adapters supply their supported native fields.</summary>
    public sealed class GameControllerSetup : Expander
    {
        /// <summary>Creates the standard initially open device-link section.</summary>
        public GameControllerSetup() { SetResourceReference(StyleProperty, typeof(Expander)); Header = "Controller setup"; IsExpanded = true; }
    }
}
