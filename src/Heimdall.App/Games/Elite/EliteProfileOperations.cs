using Heimdall.Application.Interfaces;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Games.Profiles;
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
        Func<GameWorkspace, GameProfileSession> CreateSession,
        Action<GameSessionInstall> InstallSession,
        Action<Guid?> ReloadChoices,
        Action<GameWorkspace> Remember,
        Func<bool> ConfirmLeave,
        Action StopLiveInput,
        Func<Guid, string[]> ReadReplacementInputs,
        Action<GameControllerChange> ReviewControllerChange,
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
        internal void ImportFile(string path) => Import.ExecuteAsync(path, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>Deletes the open saved configuration through the reviewed delete command.</summary>
        internal void DeleteConfiguration(Func<string, bool> confirm, Action deleted)
        {
            if (_session is null || _revision is null || _busy) return;
            new EliteDeleteConfigurationCommand(context, Input, confirm, deleted).ExecuteAsync(_session.Id, CancellationToken.None).GetAwaiter().GetResult();
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
            RequireUniqueName(name, _session.Id); ProfileName.Text = name;
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

        internal void ExportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Pending) return;
            StopLiveInput();
            // Reference repair is a reviewed controller change, a separate operation that must complete before export admission.
            var issues = _session.ReviewGameExport();
            if (issues.Count > 0) { ResolveExportIssue(issues); return; }
            var receipt = Export.ExecuteAsync(new(ExportToReviewFolder.IsChecked == true), CancellationToken.None).GetAwaiter().GetResult();
            if (!receipt.Succeeded && receipt.Message.Length > 0) Status(receipt.Message);
        });

        /// <summary>Native post-commit refresh for the shared save sequence; persistence itself is owned by <see cref="EliteProfileSaving"/>.</summary>
        internal void AfterSave()
        {
            if (_session is not { } session) return;
            var workspace = session.Snapshot;
            ReloadChoices(workspace.Id); Remember(workspace);
            ExportState.Text = "Saved in HEIMDALL. Export writes the named ED preset to your selected destination.";
            Status(SavedMessage);
        }
        /// <summary>Gets the native saved status, including a best-effort recent-selection failure.</summary>
        internal string SavedMessage => "Saved in your HEIMDALL data profile. Active Elite bindings are unchanged." + (_services.SelectionError is { } error ? " Recent selection was not saved: " + error : "");
        /// <summary>Gets the trimmed native configuration name.</summary>
        internal string CandidateName => ProfileName.Text.Trim();
        /// <summary>Installs a persisted preset model as a fresh native session.</summary>
        internal void Reopen(GameWorkspace workspace, string revision) => InstallSession(new(CreateSession(workspace), revision));
        internal string BindingsDirectory => _bindingsDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
        internal Func<string, bool>? ConfirmPresetReplacement { get; set; }
        internal Func<IReadOnlyList<GameExportIssue>, EliteExportResolution?>? ExportReview { get; set; }
        internal Func<string?>? BindingsDirectoryPicker { get; set; }
        internal Func<string, string, string?>? ConfigurationNamePicker { get; set; }
        #endregion END Public
        #region BEGIN private helpers
        private EliteImportCommand? _import;
        private EliteExportCommand? _export;
        private EliteCreateConfigurationCommand? _create;
        private IWorkspaceInputLifetime Input => new DelegateInputLifetime(StopLiveInput);
        private EliteImportCommand Import => _import ??= new(context, Input, CreateSession, InstallSession, Status);
        private EliteCreateConfigurationCommand Create => _create ??= new(context, Input, CreateSession, installed =>
        {
            InstallSession(installed); var workspace = installed.Session.Snapshot; ReloadChoices(workspace.Id); Remember(workspace);
        }, Status);
        private EliteExportCommand Export => _export ??= new(context, Input, () => BindingsDirectory,
            () => _exportPicker is null ? PickExportFolder() : _exportPicker(),
            target => ConfirmPresetReplacement?.Invoke(target) ?? ThemeMessageBox.Show(owner,
                "Replace " + target + "? An exact backup will be created. Close Elite Dangerous before continuing.",
                "Export preset", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes,
            text => ExportState.Text = text, Status);
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
                var profile = context.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(path)] = File.ReadAllText(path) });
                source = new(1, Guid.NewGuid(), "Template", profile, new());
            }
            var name = ChooseConfigurationName(clear ? "New" : "Duplicate", clear ? "New Controls" : ProfileName.Text.Trim() + " Copy");
            if (name is null) return;
            RequireUniqueName(name);
            // Duplicate includes the visible pending draft; New resolves it before leaving.
            if (clear && !ConfirmLeave()) return;
            if (clear && _session is not null) source = _session.Snapshot;
            Create.ExecuteAsync(new(source, name, clear), CancellationToken.None).GetAwaiter().GetResult();
        }

        private string? PickImport()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Elite bindings (*.binds)|*.binds",
                Title = "Import Elite controls preset",
                InitialDirectory = BindingsDirectory
            };
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
