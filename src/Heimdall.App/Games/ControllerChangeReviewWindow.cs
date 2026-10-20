using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Presents the complete controller impact in a scrollable review before staging changes.</summary>
    internal sealed class ControllerChangeReviewWindow : Window
    {
        internal ControllerChangeReviewWindow(Window owner, GameControllerChange change)
        {
            Owner = owner; Title = "Review ED controller change";
            Width = 760; Height = 560; MinWidth = 500; MinHeight = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var layout = new DockPanel { Margin = new Thickness(18) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
            var apply = new Button { Content = "Stage reviewed change", IsDefault = true };
            apply.Click += (_, _) => { DialogResult = true; };
            buttons.Children.Add(cancel); buttons.Children.Add(apply);
            DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            var preview = new TextBox { Text = change.Summary, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            System.Windows.Automation.AutomationProperties.SetName(preview, "Controller change impact");
            layout.Children.Add(preview); Content = layout;
        }
    }
}
