using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace Heimdall.App
{
    /// <summary>Shared Devices detail layout. The workspace host supplies the controller rail.</summary>
    public sealed class DeviceDetailsLayout : Grid
    {
        public DeviceDetailsLayout()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 240 });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 280 });
            var divider = new GridSplitter();
            divider.SetResourceReference(StyleProperty, "WorkspaceDivider");
            AutomationProperties.SetName(divider, "Controller setup and drawing divider");
            SetColumn(divider, 1);
            Children.Add(divider);
        }
    }
}
