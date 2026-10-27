using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>
    /// Composed four-region workspace host. Simulator views supply the game/profile
    /// chrome, configuration/feature navigation, central feature surface and status,
    /// respectively. Layout lives here rather than in each game window.
    /// This is deliberately a Grid, not a new game-specific mega-window.
    /// </summary>
    public sealed class GameWorkspaceShell : Grid
    {
        public GameWorkspaceShell()
        {
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }
}
