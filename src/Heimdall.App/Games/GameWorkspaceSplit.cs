using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Owns the resizable two-pane geometry shared by DCS and game workspace features.</summary>
    public class GameWorkspaceSplit : Grid
    {
        /// <summary>Creates the common command/workspace split and keyboard-accessible divider.</summary>
        public GameWorkspaceSplit()
        {
            ColumnDefinitions.Add(new() { Width = new GridLength(34, GridUnitType.Star), MinWidth = 230 });
            ColumnDefinitions.Add(new() { Width = new GridLength(8) });
            ColumnDefinitions.Add(new() { Width = new GridLength(66, GridUnitType.Star), MinWidth = 300 });
            var divider = new GridSplitter();
            divider.SetResourceReference(StyleProperty, "WorkspaceDivider");
            System.Windows.Automation.AutomationProperties.SetName(divider, "Workspace pane divider");
            SetColumn(divider, 1); Children.Add(divider);
        }
    }
}
