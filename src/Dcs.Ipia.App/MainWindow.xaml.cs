using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Microsoft.Win32;

namespace DcsIpia.Presentation
{
    /// <summary>Displays the themed module workspace and opens read-only repository or Saved Games previews.</summary>
    public partial class MainWindow : Window
    {
        private readonly IModuleWorkspaceLoader _loader;
        private readonly IDcsFolderImporter _folderImporter;
        private readonly IAppSettingsService _settings;
        private readonly IEditorWorkspaceRepository _workspaces;
        private ModuleWorkspaceViewModel? _lastViewModel;
        /// <summary>Initializes the workspace with injected import and settings boundaries.</summary>
        /// <param name="loader">The committed module workspace loader.</param>
        /// <param name="folderImporter">The live Saved Games profile reader.</param>
        /// <param name="settings">The validated user preference service.</param>
        /// <param name="workspaces">Persisted graphics, catalogs and assignments.</param>
        public MainWindow(IModuleWorkspaceLoader loader, IDcsFolderImporter folderImporter,
                          IAppSettingsService settings, IEditorWorkspaceRepository workspaces)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _folderImporter = folderImporter ?? throw new ArgumentNullException(nameof(folderImporter));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
            InitializeComponent();
            ThemeManager.Apply(_settings.Current.Theme.ToString());
            ThemePicker.SelectedIndex = _settings.Current.Theme == AppearanceTheme.Dark ? 1 : 0;
            ThemeSaveStatus.Text = _settings.LastError ?? string.Empty;
            Loaded += async (_, _) => await RefreshSavedModulesAsync();
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private async void ImportModule_Click(object sender, RoutedEventArgs e)
        {
            ImportButton.IsEnabled = false;
            try
            {
                var selected =
                    new OpenFolderDialog { Title =
                                               "Select module repository root or Saved Games module/joystick folder" };
                if (selected.ShowDialog(this) != true)
                    return;
                var folder = selected.FolderName;
                if (_folderImporter.HasProfiles(folder) ||
                    string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), "joystick",
                                  StringComparison.OrdinalIgnoreCase) ||
                    Directory.Exists(Path.Combine(folder, "joystick")))
                {
                    ImportStatus.Text = "Reading local Saved Games joystick profiles…";
                    var result = await _folderImporter.ImportAsync(folder);
                    if (result.Profiles.Count == 0)
                    {
                        ShowImportError(
                            "No readable joystick profiles were found. " +
                            string.Join("; ", result.Diagnostics.Where(x => x.IsError).Select(x => x.Message)));
                        return;
                    }
                    _lastViewModel = new ModuleWorkspaceViewModel(result);
                    ImportStatus.Text =
                        $"Read {result.Profiles.Count} local profiles and {result.Bindings.Count} bindings. {result.Diagnostics.Count(x=>x.IsError)} issues; open Diagnostics in the preview. Import again to reopen.";
                }
                else
                {
                    ImportStatus.Text = "Reading module sources, built-in shared hardware, and kneeboard PNGs…";
                    var preview = await _loader.LoadAsync(BundledSharedCatalog.Root, folder);
                    if (!preview.Import.Succeeded)
                    {
                        ShowImportError(
                            "Module source import needs attention: " +
                            string.Join("; ", preview.Import.Diagnostics.Where(x => x.IsError).Select(x => x.Message)));
                        return;
                    }
                    var importedView = new ModuleWorkspaceViewModel(preview);
                    if (preview.Diagrams.Any(x => x.Controls.Count > 0) && importedView.Draft is null)
                    {
                        ShowImportError("Assignments were not saved: " + importedView.DraftNote);
                        return;
                    }
                    var module = preview.Import.Configuration!;
                    var authored =
                        preview.Import.Metadata["config/kneeboard.json"].GetProperty("pages").EnumerateArray().Sum(
                            page =>
                                (page.TryGetProperty("controls", out var controls) ? controls.EnumerateObject().Count()
                                                                                   : 0) +
                                (page.TryGetProperty("layers", out var layers)
                                     ? layers.EnumerateArray().Sum(layer =>
                                                                       layer.TryGetProperty("controls", out var nested)
                                                                           ? nested.EnumerateObject().Count()
                                                                           : 0)
                                     : 0));
                    if (authored > 0 && importedView.Labels.Count == 0)
                        throw new InvalidDataException(
                            "No authored kneeboard labels matched the imported module bindings.");
                    var saved = new EditorWorkspaceSnapshot(
                        module, preview.Import.Pages, preview.Images, preview.Diagrams, importedView.ModuleCommands,
                        preview.UiCommands, importedView.Draft?.Snapshot ?? [], importedView.Labels,
                        preview.Import.Sources.Concat(preview.SharedLuaSources)
                            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
                    await _workspaces.SaveAsync(saved);
                    var persisted =
                        await _workspaces.GetAsync(module.Module.Id) ??
                        throw new InvalidDataException("The imported workspace could not be reopened from SQLite.");
                    if (persisted.Labels.Count != importedView.Labels.Count ||
                        importedView.Labels.Any(pair => !persisted.Labels.TryGetValue(pair.Key, out var value) ||
                                                        value != pair.Value))
                        throw new InvalidDataException(
                            "The authored kneeboard labels did not survive the SQLite save.");
                    _lastViewModel = new ModuleWorkspaceViewModel(persisted);
                    await RefreshSavedModulesAsync(module.Module.Id);
                    ImportStatus.Text =
                        $"Imported {module.Profiles.Count} profiles, {persisted.Labels.Count} authored kneeboard labels, {preview.Images.Count} kneeboard PNGs and {preview.Diagrams.Count} UI Layer graphics. {preview.GraphicDiagnostics.Count} graphic issues; select the module to reopen.";
                }
                ShowImported();
            }
            catch (Exception ex)
            {
                ShowImportError($"Unable to open this source: {ex.Message}");
            }
            finally
            {
                ImportButton.IsEnabled = true;
            }
        }
        private async void OpenSaved_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SavedModules.SelectedItem is not SavedModuleChoice choice)
                {
                    await RefreshSavedModulesAsync();
                    if (SavedModules.SelectedItem is not SavedModuleChoice)
                    {
                        ImportStatus.Text = "No local editor workspace has been saved yet.";
                        return;
                    }
                    choice = (SavedModuleChoice)SavedModules.SelectedItem;
                }
                var saved = await _workspaces.GetAsync(choice.Id);
                if (saved is null)
                {
                    ImportStatus.Text = "The selected module is no longer in the database.";
                    await RefreshSavedModulesAsync();
                    return;
                }
                _lastViewModel = new ModuleWorkspaceViewModel(saved);
                ShowImported();
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to open saved workspace: " + ex.Message);
            }
        }
        private async void SavedModuleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SavedModules?.SelectedItem is not SavedModuleChoice choice)
                return;
            try
            {
                var saved = await _workspaces.GetAsync(choice.Id);
                _lastViewModel = saved is null ? null : new ModuleWorkspaceViewModel(saved);
                ImportStatus.Text =
                    saved is null
                        ? "The selected module is no longer in the database."
                        : $"Selected {saved.Configuration.Module.DisplayName} ({saved.Configuration.Module.DcsId}) from SQLite.";
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to select saved module: " + ex.Message);
            }
        }
        private async Task RefreshSavedModulesAsync(Guid? selected = null)
        {
            var previous = selected ?? (SavedModules.SelectedItem as SavedModuleChoice)?.Id;
            var choices = new List<SavedModuleChoice>();
            foreach (var id in await _workspaces.ListAsync())
            {
                var snapshot = await _workspaces.GetAsync(id);
                if (snapshot is null)
                    continue;
                var module = snapshot.Configuration.Module;
                choices.Add(new(id, $"{module.DisplayName} ({module.DcsId})"));
            }
            SavedModules.ItemsSource = choices;
            SavedModules.SelectedItem = choices.FirstOrDefault(x => x.Id == previous) ?? choices.FirstOrDefault();
        }
        private void ShowImportError(string message)
        {
            ImportStatus.Text = message;
            MessageBox.Show(this, message, "Import needs attention", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        private void ShowImported()
        {
            if (_lastViewModel is null)
                return;
            var window = new ModuleWorkspaceWindow(_lastViewModel, _settings, _workspaces) { Owner = this };
            window.ShowDialog();
        }
        private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemePicker.SelectedItem is not ComboBoxItem { Tag : string theme })
                return;
            ThemeManager.Apply(theme);
            var saved = _settings.TryUpdateTheme(Enum.Parse<AppearanceTheme>(theme));
            ThemeSaveStatus.Text =
                saved ? string.Empty : _settings.LastError ?? "Theme changed for this session but was not saved.";
        }
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsWindow(_settings) { Owner = this };
            dialog.ShowDialog();
            ThemePicker.SelectedIndex = _settings.Current.Theme == AppearanceTheme.Dark ? 1 : 0;
            ThemeSaveStatus.Text = _settings.LastError ?? string.Empty;
        }
        #endregion END private helpers
    }
}
