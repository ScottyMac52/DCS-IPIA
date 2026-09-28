using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Edits the validated preferences for the current Windows user.</summary>
    public partial class SettingsWindow : Window
    {
        private readonly IAppSettingsService _settings;
        private bool _ready;

        /// <summary>Shows the effective theme and occupied-target policy.</summary>
        /// <param name="settings">The injected application settings service.</param>
        public SettingsWindow(IAppSettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeComponent();
            ThemeChoice.SelectedIndex = _settings.Current.Theme == AppearanceTheme.Dark ? 1 : 0;
            ConflictChoice.SelectedIndex = (int)_settings.Current.OccupiedDestinationBehavior;
            SaveStatus.Text = _settings.LastError ?? string.Empty;
            _ready = true;
        }

        #region BEGIN Public
        #endregion END Public

        #region BEGIN private helpers
        private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || ThemeChoice.SelectedItem is not ComboBoxItem { Tag : string value })
                return;
            var theme = Enum.Parse<AppearanceTheme>(value);
            ThemeManager.Apply(value);
            SaveStatus.Text = _settings.TryUpdateTheme(theme) ? "Theme saved." : _settings.LastError;
        }

        private void ConflictChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || ConflictChoice.SelectedItem is not ComboBoxItem { Tag : string value })
                return;
            var behavior = Enum.Parse<OccupiedDestinationBehavior>(value);
            SaveStatus.Text = _settings.TryUpdateOccupiedDestinationBehavior(behavior) ? "Destination behavior saved."
                                                                                       : _settings.LastError;
        }
        #endregion END private helpers
    }
}
