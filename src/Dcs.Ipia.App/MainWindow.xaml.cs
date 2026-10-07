using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
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
        private readonly ISharedUiLayerWorkspaceRepository? _sharedDefaults;
        private readonly Func<string?>? _snapshotFilePicker;
        private ModuleWorkspaceViewModel? _lastViewModel;
        private bool _moduleOperationBusy;
        private readonly IFlyNowService? _flyNow;
        private readonly Func<string?>? _savedGamesFolderPicker;
        private FlyNowDeployment? _activeDeployment;
        private bool _flyNowReadFailed;
        private bool _flyNowLoading = true;
        private readonly IUserDataRecoveryService? _recovery;
        private readonly Action? _closeDatabase;
        private readonly UserDataProfile? _profile;
        private readonly IUserDataProfileService? _profileService;
        private readonly HardwareInventoryManager? _hardwareInventory;
        /// <summary>Initializes the workspace with injected import and settings boundaries.</summary>
        /// <param name="loader">The committed module workspace loader.</param>
        /// <param name="folderImporter">The live Saved Games profile reader.</param>
        /// <param name="settings">The validated user preference service.</param>
        /// <param name="workspaces">Persisted graphics, catalogs and assignments.</param>
        /// <param name="sharedDefaults">Independent shared default revision storage.</param>
        /// <param name="snapshotFilePicker">Optional snapshot file selection adapter.</param>
        /// <param name="flyNow">Persistent native deployment service.</param>
        /// <param name="savedGamesFolderPicker">Optional destination browser adapter.</param>
        /// <param name="recovery">Verified full-user-data backup and restore operations.</param>
        /// <param name="closeDatabase">Quiesces services before replacing data.</param>
        /// <param name="profile">Selected production or isolated test profile.</param>
        /// <param name="profileService">Setup state and confirmed reset operations.</param>
        /// <param name="hardwareInventory">Profile-scoped independent hardware inventory.</param>
        public MainWindow(IModuleWorkspaceLoader loader, IDcsFolderImporter folderImporter,
                          IAppSettingsService settings, IEditorWorkspaceRepository workspaces,
                          ISharedUiLayerWorkspaceRepository? sharedDefaults = null, Func<string?>? snapshotFilePicker = null,
                          IFlyNowService? flyNow = null, Func<string?>? savedGamesFolderPicker = null,
                          IUserDataRecoveryService? recovery = null, Action? closeDatabase = null,
                          UserDataProfile? profile = null, IUserDataProfileService? profileService = null,
                          HardwareInventoryManager? hardwareInventory = null)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _folderImporter = folderImporter ?? throw new ArgumentNullException(nameof(folderImporter));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
            _sharedDefaults = sharedDefaults;
            _snapshotFilePicker = snapshotFilePicker;
            _flyNow = flyNow;
            _savedGamesFolderPicker = savedGamesFolderPicker;
            _recovery = recovery;
            _closeDatabase = closeDatabase;
            _profile = profile;
            _profileService = profileService;
            _hardwareInventory = hardwareInventory;
            InitializeComponent();
            BindMainCommands();
            Title = "DCS-IPIA — Modules · " + ApplicationVersion.Label;
            if (profile is not null)
                ProfileIndicator.Text = (profile.IsTest ? "TEST PROFILE: " : "Profile: ") + profile.Name + " · " + profile.DataDirectory +
                    (profile.IsTest ? " · DCS deployment disabled" : "");
            RestoreModuleSnapshotButton.IsEnabled = _workspaces is IEditorWorkspaceCreationRepository;
            SharedDefaultsButton.IsEnabled = _sharedDefaults is not null;
            NewModuleButton.IsEnabled = _sharedDefaults is not null;
            ThemeManager.Apply(_settings.Current.Theme.ToString());
            ThemePicker.SelectedIndex = _settings.Current.Theme == AppearanceTheme.Dark ? 1 : 0;
            ThemeSaveStatus.Text = _settings.LastError ?? string.Empty;
            Loaded += async (_, _) => { await RefreshSavedModulesAsync(); await RefreshFlyNowAsync(); };
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void ExecuteMainQuickAction(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: RoutedUICommand command } && command.CanExecute(null, this)) command.Execute(null, this);
        }
        private void BindMainCommands()
        {
            var actions = new Dictionary<RoutedUICommand, RoutedEventHandler>
            {
                [MainWindowCommands.NewModule] = NewModule_Click, [MainWindowCommands.OpenModule] = OpenSaved_Click,
                [MainWindowCommands.ImportModule] = ImportModule_Click, [MainWindowCommands.RestoreModule] = RestoreModuleSnapshotClicked,
                [MainWindowCommands.DeleteModule] = DeleteModule_Click, [MainWindowCommands.SharedDefaults] = SharedDefaults_Click,
                [MainWindowCommands.Settings] = Settings_Click, [MainWindowCommands.Destination] = BrowseSavedGamesClicked,
                [MainWindowCommands.FlyNow] = FlyNowClicked, [MainWindowCommands.ClearDeployment] = ClearFlyNowClicked,
                [MainWindowCommands.Backup] = OpenRecoveryClicked, [MainWindowCommands.RestoreData] = OpenRecoveryClicked,
                [MainWindowCommands.Recovery] = OpenRecoveryClicked, [MainWindowCommands.DataLocation] = ShowDataLocationClicked,
                [MainWindowCommands.Profiles] = OpenProfilesClicked,
                [MainWindowCommands.DiscoverHardware] = (_, _) => OpenHardware(true),
                [MainWindowCommands.ManageHardware] = (_, _) => OpenHardware(false),
                [MainWindowCommands.About] = AboutClicked, [MainWindowCommands.Exit] = (_, _) => Close()
            };
            foreach (var (command, action) in actions)
                CommandBindings.Add(new CommandBinding(command, (sender, e) => action(sender, e),
                    (_, e) => { e.CanExecute = CanExecuteMainCommand(command); e.Handled = true; }));
        }
        private bool CanExecuteMainCommand(RoutedUICommand command)
        {
            if (_moduleOperationBusy) return false;
            if (command == MainWindowCommands.DiscoverHardware || command == MainWindowCommands.ManageHardware)
                return _hardwareInventory is not null && OwnedWindows.Count == 0;
            if ((command == MainWindowCommands.FlyNow || command == MainWindowCommands.ClearDeployment) && _profile?.IsTest == true) return false;
            if (command == MainWindowCommands.Profiles) return _profileService is not null && !_flyNowLoading && OwnedWindows.Count == 0;
            if (command == MainWindowCommands.NewModule || command == MainWindowCommands.SharedDefaults) return _sharedDefaults is not null;
            if (command == MainWindowCommands.RestoreModule) return _workspaces is IEditorWorkspaceCreationRepository;
            var selected = (SavedModules.SelectedItem as SavedModuleChoice)?.Id;
            if (command == MainWindowCommands.OpenModule) return selected is not null;
            if (command == MainWindowCommands.DeleteModule) return selected is not null && !_flyNowReadFailed && _activeDeployment?.ModuleId != selected;
            if (command == MainWindowCommands.FlyNow) return !_flyNowLoading && !_flyNowReadFailed && _flyNow is not null && selected is not null &&
                _settings.Current.SavedGamesDcsFolder.Length > 0 && (_activeDeployment is null || (!_activeDeployment.Clearing && _activeDeployment.ModuleId == selected &&
                string.Equals(_activeDeployment.Destination, _settings.Current.SavedGamesDcsFolder, StringComparison.OrdinalIgnoreCase)));
            if (command == MainWindowCommands.ClearDeployment) return !_flyNowLoading && _flyNow is not null && (_activeDeployment is not null || _flyNowReadFailed);
            if (command == MainWindowCommands.Backup || command == MainWindowCommands.RestoreData || command == MainWindowCommands.Recovery || command == MainWindowCommands.DataLocation)
                return _recovery is not null && !_flyNowLoading && OwnedWindows.Count == 0;
            return true;
        }
        private void OpenRecoveryClicked(object sender, RoutedEventArgs e)
        {
            if (_recovery is null || _moduleOperationBusy) return;
            SetModuleOperationBusy(true);
            var dialog = new UserDataRecoveryWindow(_recovery, _closeDatabase) { Owner = this };
            try { dialog.ShowDialog(); }
            finally
            {
                if (dialog.RequiresRestart) System.Windows.Application.Current.Shutdown();
                else { _ = RefreshFlyNowAsync(); SetModuleOperationBusy(false); }
            }
        }
        private void ShowDataLocationClicked(object sender, RoutedEventArgs e)
        {
            if (_recovery is not null) MessageBox.Show(this, _recovery.DataDirectory, "IPIA user-data location", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void OpenProfilesClicked(object sender, RoutedEventArgs e)
        {
            if (_profile is null || _profileService is null || OwnedWindows.Count > 0 || _moduleOperationBusy) return;
            var dialog = new UserDataProfileWindow(_profile, _profileService, _closeDatabase ?? (() => { })) { Owner = this };
            dialog.ShowDialog();
            if (dialog.RequiresRestart) System.Windows.Application.Current.Shutdown();
        }
        private void OpenHardware(bool discover)
        {
            if (_hardwareInventory is null || OwnedWindows.Count > 0 || _moduleOperationBusy) return;
            using var dialog = new HardwareInventoryWindow(_hardwareInventory, discover) { Owner = this };
            dialog.ShowDialog();
        }
        private void AboutClicked(object sender, RoutedEventArgs e) => MessageBox.Show(this,
            "DCS-IPIA · " + ApplicationVersion.Label + "\n\nOpen a module to edit Devices, Modifiers, Assignments, Commands and Pages, then Save to database before previewing, validating, exporting a module snapshot or building a package.\n\nData & recovery backs up the entire saved user profile. Restore replaces it and requires restarting. Module snapshots instead restore one module without replacing other modules. Deployment → Fly Now/Clear writes only explicitly selected DCS payloads.",
            "About and workflow help", MessageBoxButton.OK, MessageBoxImage.Information);
        private async Task RefreshFlyNowAsync()
        {
            _flyNowLoading = true;
            UpdateFlyNowControls();
            try
            {
                _activeDeployment = _flyNow is null ? null : await _flyNow.GetActiveAsync();
                _flyNowReadFailed = false;
                FlyNowStatus.Text = _activeDeployment is null ? "No module is active in Fly Now!."
                    : $"Active: {_activeDeployment.DisplayName} → {_activeDeployment.Destination}. " +
                      (_activeDeployment.Clearing ? "Cleanup is incomplete; retry Clear." : "Clear before deploying another module. The active UI Layer belongs to this module.");
            }
            catch (Exception ex)
            {
                _flyNowReadFailed = true;
                FlyNowStatus.Text = "Fly Now! recovery needs attention: " + ex.Message;
            }
            _flyNowLoading = false;
            UpdateFlyNowControls();
        }

        private void UpdateFlyNowControls()
        {
            if (FlyNowButton is null) return;
            var selected = (SavedModules.SelectedItem as SavedModuleChoice)?.Id;
            var destination = _settings.Current.SavedGamesDcsFolder;
            SavedGamesDestination.Text = destination.Length == 0 ? "Setup: browse for your DCS Saved Games instance folder." : destination;
            FlyNowButton.IsEnabled = _profile?.IsTest != true && !_moduleOperationBusy && !_flyNowLoading && !_flyNowReadFailed && _flyNow is not null && selected is not null &&
                destination.Length > 0 && (_activeDeployment is null || (!_activeDeployment.Clearing && _activeDeployment.ModuleId == selected &&
                    string.Equals(_activeDeployment.Destination, destination, StringComparison.OrdinalIgnoreCase)));
            ClearFlyNowButton.IsEnabled = _profile?.IsTest != true && !_moduleOperationBusy && !_flyNowLoading && _flyNow is not null && (_activeDeployment is not null || _flyNowReadFailed);
            BrowseSavedGamesButton.IsEnabled = !_moduleOperationBusy;
            if (_activeDeployment?.ModuleId == selected || _flyNowReadFailed) DeleteModuleButton.IsEnabled = false;
            CommandManager.InvalidateRequerySuggested();
        }

        private async void FlyNowClicked(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy || !FlyNowButton.IsEnabled || _flyNow is null || SavedModules.SelectedItem is not SavedModuleChoice selected) return;
            SetModuleOperationBusy(true);
            try
            {
                FlyNowStatus.Text = "Validating and deploying the saved module and its UI Layer…";
                var destination = _settings.Current.SavedGamesDcsFolder;
                var active = await Task.Run(() => _flyNow.DeployAsync(selected.Id, destination));
                ImportStatus.Text = $"Fly Now! deployed {active.DisplayName} and its UI Layer ({active.Files.Count} files) to {active.Destination}.";
            }
            catch (Exception ex) { ImportStatus.Text = "Fly Now! did not complete: " + ex.Message; }
            finally { await RefreshFlyNowAsync(); SetModuleOperationBusy(false); }
        }

        private async void ClearFlyNowClicked(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy || !ClearFlyNowButton.IsEnabled || _flyNow is null) return;
            SetModuleOperationBusy(true);
            try
            {
                FlyNowStatus.Text = "Clearing the recorded Fly Now! files…";
                await _flyNow.ClearAsync();
                ImportStatus.Text = "Fly Now! cleared. Choose a module to deploy; saved authoring data is preserved.";
            }
            catch (Exception ex) { ImportStatus.Text = "Clear did not complete: " + ex.Message; }
            finally { await RefreshFlyNowAsync(); SetModuleOperationBusy(false); }
        }

        private void BrowseSavedGamesClicked(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy) return;
            var picker = new OpenFolderDialog { Title = "Select Saved Games DCS Folder", InitialDirectory = _settings.Current.SavedGamesDcsFolder };
            var folder = _savedGamesFolderPicker is not null ? _savedGamesFolderPicker() : picker.ShowDialog(this) == true ? picker.FolderName : null;
            if (folder is null) return;
            ThemeSaveStatus.Text = _settings.TryUpdateSavedGamesDcsFolder(folder) ? "Saved Games DCS Folder saved." : _settings.LastError;
            UpdateFlyNowControls();
        }

        private async void SharedDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_sharedDefaults is null || _moduleOperationBusy) return;
            SetModuleOperationBusy(true);
            try
            {
                var saved = await _sharedDefaults.GetAsync();
                if (saved is null)
                {
                    var seed = await SharedUiLayerWorkspaceFactory.CreateAsync();
                    saved = await _sharedDefaults.SaveAsync(seed);
                }
                new SharedUiLayerWorkspaceWindow(saved, _sharedDefaults, _workspaces) { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to open shared default UI Layer: " + ex.Message);
            }
            finally { SetModuleOperationBusy(false); }
        }
        private async void NewModule_Click(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy || _sharedDefaults is null)
                return;
            SetModuleOperationBusy(true);
            try
            {
                ImportStatus.Text = "Loading internal hardware and shared defaults…";
                var defaults = await _sharedDefaults.GetAsync() ??
                    await _sharedDefaults.SaveAsync(await SharedUiLayerWorkspaceFactory.CreateAsync());
                var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                var dialog = new NewModuleWindow(_workspaces, defaults, hardware) { Owner = this };
                if (dialog.ShowDialog() != true || dialog.CreatedWorkspace is not { } saved)
                    return;
                _lastViewModel = new ModuleWorkspaceViewModel(saved);
                await RefreshSavedModulesAsync(saved.Configuration.Module.Id);
                ImportStatus.Text = $"Created {saved.Configuration.Module.DisplayName}; shared default revision {defaults.Revision} is pinned.";
                ShowImported();
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to create module: " + ex.Message);
            }
            finally
            {
                SetModuleOperationBusy(false);
            }
        }
        private async void RestoreModuleSnapshotClicked(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy || _workspaces is not IEditorWorkspaceCreationRepository repository) return;
            var path = _snapshotFilePicker is null ? ChooseSnapshotFile() : _snapshotFilePicker();
            if (path is null) return;
            SetModuleOperationBusy(true);
            try
            {
                ImportStatus.Text = "Verifying module snapshot, pinned assets and generated package…";
                var json = await File.ReadAllTextAsync(path);
                var snapshot = await Task.Run(() => ModuleSnapshotWorkflow.Read(json));
                var restored = await ModuleSnapshotWorkflow.RestoreAsync(snapshot, repository);
                _lastViewModel = new ModuleWorkspaceViewModel(restored);
                await RefreshSavedModulesAsync(restored.Configuration.Module.Id);
                ImportStatus.Text = $"Restored {restored.Configuration.Module.DisplayName} from verified snapshot. Open module to edit or build with package version {snapshot.Version}. Shared defaults and other modules were preserved.";
            }
            catch (Exception ex) { ImportStatus.Text = "Module snapshot was not restored: " + ex.Message; }
            finally { SetModuleOperationBusy(false); }
        }
        private string? ChooseSnapshotFile()
        {
            var picker = new OpenFileDialog { Title = "Restore a verified IPIA module snapshot", Filter = "IPIA module snapshot|*.ipia-module.json" };
            return picker.ShowDialog(this) == true ? picker.FileName : null;
        }
        private async void ImportModule_Click(object sender, RoutedEventArgs e)
        {
            SetModuleOperationBusy(true);
            try
            {
                var selected =
                    new OpenFolderDialog
                    {
                        Title =
                                               "Select module repository root or Saved Games module/joystick folder"
                    };
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
                    if (!result.Succeeded)
                    {
                        ShowImportError("Saved Games profiles need attention: " + string.Join("; ", result.Diagnostics.Where(x => x.IsError).Select(x => x.Message)));
                        return;
                    }
                    if (_sharedDefaults is null)
                        throw new InvalidOperationException("Shared UI Layer storage is required to create an editable module.");
                    var defaults = await _sharedDefaults.GetAsync() ??
                        await _sharedDefaults.SaveAsync(await SharedUiLayerWorkspaceFactory.CreateAsync());
                    var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                    var review = new SavedGamesImportWindow(result, defaults, hardware, _workspaces) { Owner = this };
                    if (review.ShowDialog() != true || review.ImportedWorkspace is not { } persisted)
                        return;
                    _lastViewModel = new ModuleWorkspaceViewModel(persisted);
                    await RefreshSavedModulesAsync(persisted.Configuration.Module.Id);
                    ImportStatus.Text = $"Imported {result.Profiles.Count} Saved Games devices and {result.Bindings.Count} bindings into an editable module. Axis removals and captured keyboard profiles are retained. Load a command JSON or Controls HTML for the complete command palette.";
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
                    saved = PinnedUiLayerProfileRecovery.Restore(saved);
                    SnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(saved);
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
                        $"Imported {module.Profiles.Count} profiles, {persisted.Labels.Count} authored kneeboard labels, {preview.Images.Count} kneeboard PNGs and {preview.Diagrams.Count} UI Layer graphics. {preview.GraphicDiagnostics.Count} graphic issues. Imported committed revision {preview.ModuleCommit}. Commit local changes and import again to refresh.";
                    if (preview.UncommittedSourcePaths.Count > 0)
                        ImportStatus.Text += Environment.NewLine + "WARNING: Local changes were excluded: " +
                            string.Join(", ", preview.UncommittedSourcePaths) + ". Commit these files, then use Import Module again.";
                }
                ShowImported();
            }
            catch (Exception ex)
            {
                ShowImportError($"Unable to open this source: {ex.Message}");
            }
            finally
            {
                SetModuleOperationBusy(false);
            }
        }
        private async void DeleteModule_Click(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy || !DeleteModuleButton.IsEnabled || SavedModules.SelectedItem is not SavedModuleChoice choice)
                return;
            if (MessageBox.Show(this,
                $"Delete {choice.DisplayName} and all of its saved profiles, assignments, labels, and kneeboards from the local database? You can import it again from scratch.",
                "Delete module", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            SetModuleOperationBusy(true);
            try
            {
                var deleted = await _workspaces.DeleteAsync(choice.Id);
                _lastViewModel = null;
                await RefreshSavedModulesAsync();
                ImportStatus.Text = deleted
                    ? $"Deleted {choice.DisplayName} from the local database. Use Import module sources to start from scratch."
                    : "The selected module is no longer in the database.";
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to delete module: " + ex.Message);
            }
            finally
            {
                SetModuleOperationBusy(false);
            }
        }
        private void SetModuleOperationBusy(bool busy)
        {
            _moduleOperationBusy = busy;
            SavedModules.IsEnabled = !busy;
            ImportButton.IsEnabled = !busy;
            NewModuleButton.IsEnabled = !busy && _sharedDefaults is not null;
            OpenModuleButton.IsEnabled = !busy;
            RestoreModuleSnapshotButton.IsEnabled = !busy && _workspaces is IEditorWorkspaceCreationRepository;
            DeleteModuleButton.IsEnabled = !busy && SavedModules.SelectedItem is SavedModuleChoice;
            SharedDefaultsButton.IsEnabled = !busy && _sharedDefaults is not null;
            ThemePicker.IsEnabled = !busy;
            CommandManager.InvalidateRequerySuggested();
            UpdateFlyNowControls();
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
                if (_moduleOperationBusy || (SavedModules.SelectedItem as SavedModuleChoice)?.Id != choice.Id)
                    return;
                if (saved is null)
                {
                    ImportStatus.Text = "The selected module is no longer in the database.";
                    await RefreshSavedModulesAsync();
                    return;
                }
                _lastViewModel = new ModuleWorkspaceViewModel(saved);
                ImportStatus.Text = SavedWorkspaceStatus(saved);
                ShowImported();
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to open saved workspace: " + ex.Message);
            }
        }
        private async void SavedModuleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeleteModuleButton is not null)
                DeleteModuleButton.IsEnabled = !_moduleOperationBusy && SavedModules?.SelectedItem is SavedModuleChoice;
            UpdateFlyNowControls();
            if (_moduleOperationBusy)
                return;
            if (SavedModules?.SelectedItem is not SavedModuleChoice choice)
            {
                _lastViewModel = null;
                return;
            }
            try
            {
                var saved = await _workspaces.GetAsync(choice.Id);
                if (_moduleOperationBusy || (SavedModules.SelectedItem as SavedModuleChoice)?.Id != choice.Id)
                    return;
                _lastViewModel = saved is null ? null : new ModuleWorkspaceViewModel(saved);
                ImportStatus.Text =
                    saved is null
                        ? "The selected module is no longer in the database."
                        : SavedWorkspaceStatus(saved);
            }
            catch (Exception ex)
            {
                ShowImportError("Unable to select saved module: " + ex.Message);
            }
        }
        private static string SavedWorkspaceStatus(EditorWorkspaceSnapshot saved) =>
            $"Opened saved workspace for {saved.Configuration.Module.DisplayName} ({saved.Configuration.Provenance}). This workspace loads from SQLite. Use Import Module only to refresh an imported repository.";
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
            if (ThemePicker.SelectedItem is not ComboBoxItem { Tag: string theme })
                return;
            ThemeManager.Apply(theme);
            var saved = _settings.TryUpdateTheme(Enum.Parse<AppearanceTheme>(theme));
            ThemeSaveStatus.Text =
                saved ? string.Empty : _settings.LastError ?? "Theme changed for this session but was not saved.";
        }
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_moduleOperationBusy) return;
            var dialog = new SettingsWindow(_settings) { Owner = this };
            dialog.ShowDialog();
            ThemePicker.SelectedIndex = _settings.Current.Theme == AppearanceTheme.Dark ? 1 : 0;
            ThemeSaveStatus.Text = _settings.LastError ?? string.Empty;
            UpdateFlyNowControls();
        }
        #endregion END private helpers
    }
}
