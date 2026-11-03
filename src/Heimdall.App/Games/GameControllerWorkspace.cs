using System.Windows;

namespace Heimdall.App.Games
{
    /// <summary>Uses one controller-sidebar sizing policy for all game workspaces.</summary>
    public sealed class GameControllerWorkspace : GameWorkspaceSplit
    {
        /// <summary>Creates the standard controller list and feature-content split.</summary>
        public GameControllerWorkspace()
        {
            ColumnDefinitions[0].Width = new GridLength(18, GridUnitType.Star); ColumnDefinitions[0].MinWidth = 160;
            ColumnDefinitions[2].Width = new GridLength(82, GridUnitType.Star); ColumnDefinitions[2].MinWidth = 540;
        }
    }
}
