using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Collects a validated preset name using the shared application controls.</summary>
    internal sealed class GameConfigurationNameWindow : Window
    {
        internal string ConfigurationName { get; private set; } = "";
        internal GameConfigurationNameWindow(Window owner, string action, string initial)
        {
            Owner = owner; Title = action + " configuration"; Width = 480; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var panel = new StackPanel { Margin = new Thickness(18) };
            var label = new TextBlock { Text = "Configuration and exported ED preset name:" };
            label.Style = (Style)FindResource("AppLabel"); panel.Children.Add(label);
            var name = new TextBox { Text = initial, MaxLength = 100, Margin = new Thickness(0, 8, 0, 8) };
            name.Style = (Style)FindResource("AppTextBox"); panel.Children.Add(name);
            System.Windows.Automation.AutomationProperties.SetName(name, "Configuration name");
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            error.Style = (Style)FindResource("AppLabel"); panel.Children.Add(error);
            var buttons = new WrapPanel();
            var accept = new Button { Content = action, IsDefault = true, Style = (Style)FindResource("ToolbarActionButton") };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Style = (Style)FindResource("ToolbarActionButton") };
            accept.Click += (_, _) =>
            {
                try { GameConfigurationName.Validate(name.Text.Trim()); ConfigurationName = name.Text.Trim(); DialogResult = true; }
                catch (InvalidOperationException invalid) { error.Text = invalid.Message; }
            };
            buttons.Children.Add(accept); buttons.Children.Add(cancel); panel.Children.Add(buttons); Content = panel;
            Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
        }
    }
}
