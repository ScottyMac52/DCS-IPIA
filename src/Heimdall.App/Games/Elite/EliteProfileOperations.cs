using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Microsoft.Win32;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Owns ED configuration import/create/rename, profile persistence and reviewed native preset export.</summary>
    internal sealed class EliteProfileOperations(
        EliteWorkspaceContext context,
        Window owner,
        ComboBox ProfilePicker,
        TextBox ProfileName,
        TextBox BindingsDirectoryText,
        CheckBox ExportToReviewFolder,
        TextBlock ExportState,
        Func<string?>? _importPicker,
        Func<string?>? _exportPicker,
        Action<GameWorkspace, string?> OpenSession,
        Action<Guid?> ReloadChoices,
        Action<GameWorkspace> Remember,
        Func<bool> ConfirmLeave,
        Action StopLiveInput,
        Func<Guid, string[]> ReadReplacementInputs,
        Action<GameControllerChange> ReviewControllerChange,
        Action<Action> RunBusy,
        Action<Action> Run,
        Action<string> Status)
    {
        private string? _bindingsDirectoryOverride = context.Services.Recent().EliteBindingsDirectory;
        private GameProfileSession? _session => context.Session;
        private string? _revision { get => context.Revision; set => context.Revision = value; }
        private bool _busy => context.Busy;
        private GameWorkspaceServices _services => context.Services;
        private bool Pending => context.Chrome.Pending;
        #region BEGIN Public
        internal void ImportFile(string path)
        {
            var profile = _services.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(path)] = File.ReadAllText(path) });
            var baseName = profile.Name.Length > 80 ? profile.Name[..80] + " Controls" : profile.Name + " Controls";
            var used = _services.Store.List().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var name = baseName; var suffix = 2;
            while (used.Contains(name)) name = baseName + " " + suffix++;
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name, profile, new());
            OpenSession(workspace, null); Status("Imported bindings. Associate your hardware drawing, then review and save this profile.");
        }

        internal void SetBindingsDirectory(string? directory)
        {
            var selection = _services.Recent() with { EliteBindingsDirectory = directory is null ? null : Path.GetFullPath(directory) };
            GameWorkspaceContract.ValidateSelection(selection); _services.Remember(selection);
            _bindingsDirectoryOverride = selection.EliteBindingsDirectory; BindingsDirectoryText.Text = BindingsDirectory;
            Status(_services.SelectionError is { } error ? "Export directory changed for this session; preference could not be saved: " + error : "Export directory: " + BindingsDirectory);
        }

        internal void NewConfigurationClicked(object sender, RoutedEventArgs e) => Run(() => CreateConfiguration(true));

        internal void DuplicateConfigurationClicked(object sender, RoutedEventArgs e) => Run(() => CreateConfiguration(false));

        internal void RenameConfigurationClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || _busy) return;
            var name = ChooseConfigurationName("Rename", ProfileName.Text.Trim());
            if (name is null) return;
            RequireUniqueName(name, _session.Snapshot.Id); ProfileName.Text = name;
            Status("Configuration name staged. Save to keep it; Export uses the saved name. Previously exported presets are retained.");
        });

        internal void BrowseBindingsDirectoryClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var picker = new OpenFolderDialog { Title = "Choose Elite bindings export directory", InitialDirectory = BindingsDirectory };
            var path = BindingsDirectoryPicker is null ? picker.ShowDialog(owner) == true ? picker.FolderName : null : BindingsDirectoryPicker();
            if (path is not null) SetBindingsDirectory(path);
        });

        internal void ResetBindingsDirectoryClicked(object sender, RoutedEventArgs e) => Run(() => SetBindingsDirectory(null));

        internal void ImportClicked(object sender, RoutedEventArgs e)
        {
            if (!ConfirmLeave()) return;
            Run(() => { var path = _importPicker is null ? PickImport() : _importPicker(); if (path is not null) { ImportFile(path); ProfilePicker.SelectedIndex = -1; } });
        }

        internal void ExportClicked(object sender, RoutedEventArgs e) => Run(() => RunBusy(() =>
        {
            if (_session is null || Pending) return;
            StopLiveInput();
            var issues = _session.ReviewGameExport();
            if (issues.Count > 0) { ResolveExportIssue(issues); return; }
            var plan = _session.ExportForGame();
            if (ExportToReviewFolder.IsChecked == true)
            {
                var parent = _exportPicker is null ? PickExportFolder() : _exportPicker();
                if (parent is null) return;
                plan = _session.ExportForGame();
                var active = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
                var output = GameReviewExport.Write(parent, plan.Files, [active, BindingsDirectory]);
                ExportState.Text = "Exported separate review copy: " + output + "\nInstallation into ED is manual. With ED closed, copy the reviewed preset into Bindings, restart ED and select " + _session.Snapshot.Name + ".";
            }
            else
            {
                var prepared = GameBindingsExport.Prepare(BindingsDirectory, plan.Files);
                var target = Path.Combine(prepared.Directory, prepared.FileName);
                if (prepared.ExpectedHash is not null && !(ConfirmPresetReplacement?.Invoke(target) ?? ThemeMessageBox.Show(owner,
                    "Replace " + target + "? An exact backup will be created. Close Elite Dangerous before continuing.",
                    "Export preset", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes))
                { Status("Export cancelled; existing preset unchanged."); return; }
                var fresh = _session.ExportForGame();
                if (Pending || !fresh.Files.TryGetValue(prepared.FileName, out var content) || content != prepared.Content)
                    throw new InvalidOperationException("The profile changed during export. Save and export again.");
                var backup = GameBindingsExport.Write(prepared);
                ExportState.Text = "Exported: " + target + (backup is null ? "" : "\nBackup: " + backup)
                    + "\nRestart ED and select " + _session.Snapshot.Name + ". Existing preset selectors are unchanged.";
            }
            Status("Export complete. " + string.Join(" ", plan.Diagnostics.Select(x => x.Message)));
        }));

        internal void SaveCore()
        {
            if (_session is null) throw new InvalidOperationException("Open an Elite configuration before saving.");
                var workspace = _session.Snapshot with { Name = ProfileName.Text.Trim() };
                GameWorkspaceContract.Validate(workspace);
                GameConfigurationName.Validate(workspace.Name);
                _revision = _services.Store.Save(workspace, _revision);
                OpenSession(workspace, _revision); ReloadChoices(workspace.Id); Remember(workspace);
                ExportState.Text = "Saved in HEIMDALL. Export writes the named ED preset to your selected destination.";
                Status("Saved in your HEIMDALL data profile. Active Elite bindings are unchanged." + (_services.SelectionError is { } error ? " Recent selection was not saved: " + error : ""));
        }
        internal string BindingsDirectory => _bindingsDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
        internal Func<string, bool>? ConfirmPresetReplacement { get; set; }
        internal Func<IReadOnlyList<GameExportIssue>, EliteExportResolution?>? ExportReview { get; set; }
        internal Func<string?>? BindingsDirectoryPicker { get; set; }
        internal Func<string, string, string?>? ConfigurationNamePicker { get; set; }
        #endregion END Public
        #region BEGIN private helpers
        private string? ChooseConfigurationName(string action, string initial)
        {
            if (ConfigurationNamePicker is not null) return ConfigurationNamePicker(action, initial);
            var dialog = new GameConfigurationNameWindow(owner, action, initial);
            return dialog.ShowDialog() == true ? dialog.ConfigurationName : null;
        }

        private void RequireUniqueName(string name, Guid? except = null)
        {
            GameConfigurationName.Validate(name);
            if (_services.Store.List().Any(x => x.Id != except && x.BackendId == "elite-dangerous"
                && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A configuration with that name already exists. Choose a different name.");
        }

        private void CreateConfiguration(bool clear)
        {
            if (_busy || (!clear && _session is null)) return;
            var source = _session?.Snapshot;
            if (source is null)
            {
                var path = _importPicker is null ? PickImport() : _importPicker();
                if (path is null) return;
                var profile = _services.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(path)] = File.ReadAllText(path) });
                source = new(1, Guid.NewGuid(), "Template", profile, new());
            }
            var name = ChooseConfigurationName(clear ? "New" : "Duplicate", clear ? "New Controls" : ProfileName.Text.Trim() + " Copy");
            if (name is null) return;
            RequireUniqueName(name);
            // Duplicate includes the visible pending draft; New resolves it before leaving.
            if (clear && !ConfirmLeave()) return;
            if (clear && _session is not null) source = _session.Snapshot;
            var workspace = GameConfigurations.Create(source, name, clear);
            var revision = _services.Store.Save(workspace, null);
            OpenSession(workspace, revision); ReloadChoices(workspace.Id); Remember(workspace);
            Status(clear ? "New configuration saved with assignments cleared. Controller setup and game settings retained."
                : "Independent configuration copy saved, including your current assignment edits.");
        }

        private string? PickImport()
        {
            var dialog = new OpenFileDialog { Filter = "Elite bindings (*.binds)|*.binds", Title = "Import Elite controls preset",
                InitialDirectory = BindingsDirectory };
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        private void ResolveExportIssue(IReadOnlyList<GameExportIssue> issues)
        {
            EliteExportResolution? resolution;
            if (ExportReview is not null) resolution = ExportReview(issues);
            else
            {
                var connected = _services.Discover().Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray();
                var dialog = new EliteExportReviewWindow(owner, issues, connected, guid => _session!.ControllerReferences(guid)
                    .Select(id => new GameDeviceChoice(id, EliteWorkspaceContext.ControllerName(_session.Snapshot, id))).ToArray(), EditionAccess.Policy.HasPremium);
                resolution = dialog.ShowDialog() == true ? dialog.Resolution : null;
            }
            if (resolution is null) { Status("Export cancelled; no files written."); return; }
            if (!issues.Any(x => x.DeviceId == resolution.DeviceId && x.CanRepair)) throw new InvalidOperationException("Choose an editable unresolved controller reference.");
            var change = resolution.Clear ? _session!.PreviewRemoveController(resolution.DeviceId)
                : _session!.PreviewReplaceController(resolution.DeviceId, resolution.PhysicalGuid,
                    () => ReadReplacementInputs(resolution.PhysicalGuid), resolution.TargetReference);
            ReviewControllerChange(change);
            if (_session!.HasChanges) Status("Reviewed repair staged; no files exported. Save, then export again to check remaining references.");
        }

        private string? PickExportFolder()
        {
            var picker = new OpenFolderDialog { Title = "Choose a parent folder for a new review export" };
            return picker.ShowDialog(owner) == true ? picker.FolderName : null;
        }
        #endregion END private helpers
    }
}
