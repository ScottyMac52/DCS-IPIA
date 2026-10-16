using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DcsIpia.Presentation
{
    /// <summary>Displays application-owned confirmations using the active palette and familiar message-box results.</summary>
    public static class ThemeMessageBox
    {
        #region BEGIN Public
        /// <summary>Shows a themed message owned by the active application window when available.</summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            Show(System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive),
                message, caption, buttons, image, defaultResult);

        /// <summary>Shows a themed confirmation while preserving the caller's explicit default and result policy.</summary>
        public static MessageBoxResult Show(Window? owner, string message, string caption,
            MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            // Logging/bootstrap failures can occur before WPF resources exist. Retain the OS emergency path.
            if (System.Windows.Application.Current?.TryFindResource("WindowBackgroundBrush") is not Brush)
                return owner is null ? MessageBox.Show(message, caption, buttons, image, defaultResult)
                    : MessageBox.Show(owner, message, caption, buttons, image, defaultResult);
            var choices = buttons switch {
                MessageBoxButton.OK => new[] { MessageBoxResult.OK },
                MessageBoxButton.OKCancel => [MessageBoxResult.OK, MessageBoxResult.Cancel],
                MessageBoxButton.YesNo => [MessageBoxResult.Yes, MessageBoxResult.No],
                MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel],
                _ => throw new ArgumentOutOfRangeException(nameof(buttons))
            };
            var result = buttons == MessageBoxButton.OK ? MessageBoxResult.OK :
                buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
            var dialog = new Window { Name = "ThemeMessageDialog", Title = caption, Width = 560, MinWidth = 380,
                SizeToContent = SizeToContent.Height, MaxHeight = SystemParameters.WorkArea.Height * 0.8,
                WindowStartupLocation = owner?.IsVisible == true ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                ShowInTaskbar = owner is null };
            if (owner?.IsVisible == true) dialog.Owner = owner;
            var panel = new DockPanel { Margin = new Thickness(20) };
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
            var content = new StackPanel();
            if (image != MessageBoxImage.None)
            {
                var heading = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8),
                    Text = image switch { MessageBoxImage.Error => "Error", MessageBoxImage.Warning => "Warning",
                        MessageBoxImage.Question => "Confirmation", _ => "Information" } };
                heading.SetResourceReference(TextBlock.ForegroundProperty,
                    image is MessageBoxImage.Error or MessageBoxImage.Warning ? "ErrorTextBrush" : "AccentTextBrush");
                content.Children.Add(heading);
            }
            content.Children.Add(new TextBox { Name = "MessageText", Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0), AcceptsReturn = true, Background = Brushes.Transparent });
            panel.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            dialog.Content = panel;
            Button? focused = null;
            foreach (var choice in choices)
            {
                var button = new Button { Content = "_" + choice, Tag = choice, MinWidth = 80, Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(8, 0, 0, 0), IsDefault = choice == (defaultResult == MessageBoxResult.None ? choices[0] : defaultResult),
                    IsCancel = choice == MessageBoxResult.Cancel || buttons == MessageBoxButton.YesNo && choice == MessageBoxResult.No ||
                        buttons == MessageBoxButton.OK };
                button.Click += (_, _) => { result = choice; dialog.DialogResult = true; };
                if (button.IsDefault) focused = button;
                actions.Children.Add(button);
            }
            dialog.Loaded += (_, _) => focused?.Focus();
            dialog.ShowDialog();
            return result;
        }
        #endregion END Public
    }
}
