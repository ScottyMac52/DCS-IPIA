using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Edits the inversion and percentage deadzone supported by Elite's native axis bindings.</summary>
    internal sealed class EliteAxisTuningWindow : Window
    {
        internal CheckBox Invert { get; }
        internal TextBox Deadzone { get; }
        private readonly TextBlock _validation = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

        internal EliteAxisTuningWindow(Window owner, string command, bool invert, float deadzone)
        {
            Owner = owner; Title = "Tune axis · " + command;
            Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var layout = new StackPanel { Margin = new Thickness(18) };
            layout.Children.Add(new TextBlock { Text = command, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            Invert = new CheckBox { Content = "Invert axis", IsChecked = invert, Margin = new Thickness(0, 12, 0, 8) };
            Invert.SetResourceReference(StyleProperty, "AppCheckBox");
            layout.Children.Add(Invert);
            layout.Children.Add(new TextBlock { Text = "Deadzone (%)", Margin = new Thickness(0, 0, 0, 4) });
            Deadzone = new TextBox { Text = deadzone.ToString(CultureInfo.CurrentCulture) };
            Deadzone.SetResourceReference(StyleProperty, "AppTextBox");
            System.Windows.Automation.AutomationProperties.SetName(Deadzone, "Axis deadzone percentage");
            layout.Children.Add(Deadzone); layout.Children.Add(_validation);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
            var apply = new Button { Content = "Apply", IsDefault = true };
            cancel.SetResourceReference(StyleProperty, "ToolbarActionButton");
            apply.SetResourceReference(StyleProperty, "ToolbarActionButton");
            apply.Click += (_, _) => { if (TryRead(out _, out _)) DialogResult = true; };
            buttons.Children.Add(cancel); buttons.Children.Add(apply); layout.Children.Add(buttons);
            Content = layout;
        }
        #region BEGIN Public
        /// <summary>Validates native Elite axis values before any assignment is staged.</summary>
        internal bool TryRead(out bool invert, out float deadzone)
        {
            invert = Invert.IsChecked == true;
            if (!float.TryParse(Deadzone.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out deadzone) ||
                !float.IsFinite(deadzone) || deadzone < 0 || deadzone > 100)
            {
                _validation.Text = "Enter a deadzone from 0 to 100 percent.";
                return false;
            }
            _validation.Text = "";
            return true;
        }
        #endregion END Public
    }
}
