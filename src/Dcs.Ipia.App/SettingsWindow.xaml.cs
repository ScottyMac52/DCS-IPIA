using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Microsoft.Win32;

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
            ThemeChoice.SelectedIndex = (int)EditionAccess.Policy.EffectiveTheme(_settings.Current.Theme);
            ThemeChoice.IsEnabled = ResetAppearance.IsEnabled = EditionAccess.Policy.IsOwner;
            EditionStatus.Text = EditionAccess.Policy.Label;
            ConflictChoice.SelectedIndex = (int)_settings.Current.OccupiedDestinationBehavior;
            SaveStatus.Text = _settings.LastError ?? AppearanceManager.LastError ?? EditionAccess.Store.LastError ?? string.Empty;
            CustomizeAppearance.IsEnabled = EditionAccess.Policy.IsOwner && AppearanceManager.Store is not null;
            SavedGamesFolder.Text = _settings.Current.SavedGamesDcsFolder;
            _ready = true;
        }

        #region BEGIN Public
        #endregion END Public

        #region BEGIN private helpers
        private void InstallOwnerLicenseClicked(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog { Title = "Install signed owner license", Filter = "Owner license JSON|*.json", CheckFileExists = true };
            if (picker.ShowDialog(this) != true) return;
            SaveStatus.Text = EditionAccess.Store.Import(picker.FileName)
                ? "Owner license installed. Restart DCS-IPIA to unlock all features and restore your saved appearance."
                : EditionAccess.Store.LastError;
        }

        private void BrowseSavedGamesClicked(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFolderDialog { Title = "Select Saved Games DCS Folder", InitialDirectory = _settings.Current.SavedGamesDcsFolder };
            if (picker.ShowDialog(this) != true) return;
            SaveStatus.Text = _settings.TryUpdateSavedGamesDcsFolder(picker.FolderName) ? "Saved Games DCS Folder saved." : _settings.LastError;
            SavedGamesFolder.Text = _settings.Current.SavedGamesDcsFolder;
        }

        private void CustomizeAppearanceClicked(object sender, RoutedEventArgs e)
        {
            if (!EditionAccess.Policy.IsOwner) return;
            if (AppearanceManager.Store is { } store) new AppearanceWindow(store) { Owner = this }.ShowDialog();
            SaveStatus.Text = AppearanceManager.LastError ?? "Appearance preview closed.";
        }

        private void ResetAppearanceClicked(object sender, RoutedEventArgs e)
        {
            if (!EditionAccess.Policy.IsOwner) return;
            if (AppearanceManager.Store is { } store && !store.TrySave(new AppearanceSettings()))
            { SaveStatus.Text = store.LastError; return; }
            AppearanceManager.Restore();
            // Persist even when Light is already selected, so a previous save failure can be retried.
            _ready = false;
            ThemeChoice.SelectedIndex = (int)AppearanceTheme.Light;
            _ready = true;
            ThemeManager.Apply(nameof(AppearanceTheme.Light));
            SaveStatus.Text = _settings.TryUpdateTheme(AppearanceTheme.Light) ? "Default appearance saved." : _settings.LastError;
        }

        private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!EditionAccess.Policy.IsOwner || !_ready || ThemeChoice.SelectedItem is not ComboBoxItem { Tag: string value })
                return;
            var theme = Enum.Parse<AppearanceTheme>(value);
            ThemeManager.Apply(value);
            SaveStatus.Text = _settings.TryUpdateTheme(theme) ? AppearanceManager.LastError ?? "Theme saved." : _settings.LastError;
        }

        private void ConflictChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || ConflictChoice.SelectedItem is not ComboBoxItem { Tag: string value })
                return;
            var behavior = Enum.Parse<OccupiedDestinationBehavior>(value);
            SaveStatus.Text = _settings.TryUpdateOccupiedDestinationBehavior(behavior) ? "Destination behavior saved."
                                                                                       : _settings.LastError;
        }
        #endregion END private helpers
    }
}
