using System.Globalization;
using System.Windows;
using Dcs.Ipia.Domain;

namespace DcsIpia.Presentation
{
    /// <summary>Edits sparse DCS axis filter values before staging them in a module draft.</summary>
    public partial class AxisTuningWindow : Window
    {
        /// <summary>Displays the current axis filter, including unset boolean fields.</summary>
        /// <param name="filter">Current tuning or null if none is stored.</param>
        public AxisTuningWindow(AxisFilter? filter)
        {
            InitializeComponent();
            Deadzone.Text = Format(filter?.Deadzone);
            SaturationX.Text = Format(filter?.SaturationX);
            SaturationY.Text = Format(filter?.SaturationY);
            Curvature.Text =
                filter?.Curvature is null
                    ? ""
                    : string.Join(", ", filter.Curvature.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
            Invert.IsChecked = filter?.Invert;
            Slider.IsChecked = filter?.Slider;
            HardwareDetent.IsChecked = filter?.HardwareDetent;
            HardwareDetentAB.Text = Format(filter?.HardwareDetentAB);
            HardwareDetentMax.Text = Format(filter?.HardwareDetentMax);
            Filter = filter;
        }
        #region BEGIN Public
        /// <summary>Gets the applied filter, or null when tuning was cleared.</summary>
        public AxisFilter? Filter { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private static string Format(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";
        private static double? Number(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !double.IsFinite(value))
                throw new FormatException("Use finite numbers with a dot as the decimal separator.");
            return value;
        }
        private void ApplyClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var curvature = string.IsNullOrWhiteSpace(Curvature.Text)
                                    ? null
                                    : Curvature.Text.Split(',', StringSplitOptions.TrimEntries)
                                          .Select(x => Number(x) ?? throw new FormatException(
                                                                        "Curvature needs a number for each entry."))
                                          .ToArray();
                Filter = new AxisFilter(Number(Deadzone.Text), Number(SaturationX.Text), Number(SaturationY.Text),
                                        curvature, Invert.IsChecked, Slider.IsChecked, HardwareDetent.IsChecked,
                                        Number(HardwareDetentAB.Text), Number(HardwareDetentMax.Text));
                if (Filter is {
                        Deadzone : null,
                        SaturationX : null,
                        SaturationY : null,
                        Curvature : null,
                        Invert : null,
                        Slider : null,
                        HardwareDetent : null,
                        HardwareDetentAB : null,
                        HardwareDetentMax : null
                    })
                    Filter = null;
                DialogResult = true;
            }
            catch (FormatException ex)
            {
                ValidationMessage.Text = ex.Message;
            }
        }
        private void ClearClicked(object sender, RoutedEventArgs e)
        {
            Filter = null;
            DialogResult = true;
        }
        #endregion END private helpers
    }
}
