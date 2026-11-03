using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Edits the inversion and percentage deadzone supported by Elite's native axis bindings.</summary>
    internal sealed class EliteAxisTuningWindow
    {
        internal CheckBox Invert { get; }
        internal TextBox Deadzone { get; }
        private readonly AxisTuningWindow _dialog;

        internal EliteAxisTuningWindow(Window owner, string command, bool invert, float deadzone)
        {
            Invert = new CheckBox { Content = "Invert axis", IsChecked = invert, Margin = new Thickness(0, 8, 0, 8) };
            Invert.SetResourceReference(FrameworkElement.StyleProperty, "AppCheckBox");
            Deadzone = new TextBox { Text = deadzone.ToString(CultureInfo.CurrentCulture) };
            Deadzone.SetResourceReference(FrameworkElement.StyleProperty, "AppTextBox");
            System.Windows.Automation.AutomationProperties.SetName(Deadzone, "Axis deadzone percentage");
            var fields = new StackPanel();
            fields.Children.Add(Invert);
            fields.Children.Add(new TextBlock { Text = "Deadzone (%)", Margin = new Thickness(0, 0, 0, 4) });
            fields.Children.Add(Deadzone);
            _dialog = new AxisTuningWindow(owner, command, fields,
                () => TryRead(out _, out _) ? null : "Enter a deadzone from 0 to 100 percent.");
        }
        #region BEGIN Public
        internal bool? ShowDialog() => _dialog.ShowDialog();
        /// <summary>Validates native Elite axis values before any assignment is staged.</summary>
        internal bool TryRead(out bool invert, out float deadzone)
        {
            invert = Invert.IsChecked == true;
            if (!float.TryParse(Deadzone.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out deadzone) ||
                !float.IsFinite(deadzone) || deadzone < 0 || deadzone > 100)
            {
                return false;
            }
            return true;
        }
        #endregion END Public
    }
}
