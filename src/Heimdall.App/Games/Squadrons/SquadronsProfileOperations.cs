using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Domain.MultiSim;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Microsoft.Win32;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Owns native profile creation, template recovery and review-only export.</summary>
    internal sealed class SquadronsProfileOperations(
        SquadronsWorkspaceContext context,
        Func<SquadronsControllerPresenter> controllers,
        Action<bool> refresh,
        Action<Action> run,
        Func<bool> confirmLeave,
        TextBlock StatusText,
        ComboBox ProfilePicker,
        Button NewProfileButton,
        SquadronsAssignmentsView _assignmentsView,
        Window owner)
    {
        /// <summary>Supplies an unattended native configuration-name prompt.</summary>
        #region BEGIN Public
        internal Func<string, string, string?>? ConfigurationNamePicker { get; set; }
        /// <summary>Native post-commit refresh for the shared save sequence; persistence itself is owned by <see cref="SquadronsProfileSaving"/>.</summary>
        internal void AfterSave()
        {
            Remember(); ReloadChoices(); refresh(true);
            _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "";
            StatusText.Text = "Squadrons profile saved.";
        }
        #endregion END Public
        #region BEGIN private helpers
        internal void ImportFile(string path)
        {
            if (!confirmLeave()) return;
            Import.ExecuteAsync(path, CancellationToken.None).GetAwaiter().GetResult();
        }

        private void InstallImport(SquadronsImportResult imported)
        {
            var opened = imported.Session;
            context.Selection.Reset();
            context.FocusedConcept = null;
            context.InspectedControl = null;
            _assignmentsView.CommandBrowser.CommandsList.SelectedItem = null;
            _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "";
            context.Session = opened; context.Revision = null; context.SourceDirectory = imported.SourceDirectory;
            context.Services.Remember(context.Services.Recent() with { BackendId = context.Backend.SimId, WorkspaceId = null, DisplayName = "" });
            ReloadChoices(); refresh(true); StatusText.Text = "Controls imported. Save this profile before exporting.";
        }

        internal void ImportTemplatesFile(string path)
        {
            if (context.Busy) throw new InvalidOperationException("Another workspace operation is still running.");
            if (context.Session is null) throw new InvalidOperationException("Open a Squadrons configuration before importing templates.");
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Squadrons controls exceed the 4 MiB import limit.");
            controllers().StopLiveInput();
            context.Session.ImportAssignmentTemplates(new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(path) });
            refresh(false);
            StatusText.Text = "Assignment templates imported. Current bindings and physical links are unchanged. Save to retain the templates.";
        }

        internal bool NewProfile(string name)
        {
            if (context.Session is null) throw new InvalidOperationException("Import native Squadrons settings or open a saved profile first.");
            GameConfigurationName.Validate(name);
            if (context.Services.Store.List().Any(entry => entry.BackendId == context.Backend.SimId &&
                string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A Squadrons profile with that name already exists.");
            if (!confirmLeave()) return false;
            var sourceDirectory = context.SourceDirectory;
            var receipt = NewProfileCommand.ExecuteAsync(name, CancellationToken.None).GetAwaiter().GetResult();
            context.SourceDirectory = sourceDirectory;
            StatusText.Text = receipt.Message;
            return true;
        }

        internal string ExportReview(string parent) => Export.ExecuteAsync(parent, CancellationToken.None).GetAwaiter().GetResult().Detail;

        private SquadronsImportCommand? _import;
        private SquadronsExportCommand? _export;
        private SquadronsNewProfileCommand? _newProfile;
        private IWorkspaceInputLifetime Input => new DelegateInputLifetime(() => controllers().StopLiveInput());
        private SquadronsImportCommand Import => _import ??= new(context, Input, CreateSession, InstallImport);
        private SquadronsExportCommand Export => _export ??= new(context, Input, message => StatusText.Text = message);
        private SquadronsNewProfileCommand NewProfileCommand => _newProfile ??= new(context, Input, LoadProfile);

        private GameProfileSession CreateSession(GameWorkspace workspace)
        {
            if (workspace.Profile.BackendId != context.Backend.SimId) throw new InvalidDataException("This is not a Squadrons profile.");
            return new(context.Backend, workspace, EditionAccess.Policy, context.Drawings.Select(x => x.DeviceId).ToArray(), context.Services.Discover, controllerInputs: () => context.ControllerInputs);
        }

        internal void LoadProfile(Guid id)
        {
            controllers().StopLiveInput();
            var saved = context.Services.Store.Load(id); var opened = CreateSession(saved.Workspace);
            context.Selection.Reset();
            context.FocusedConcept = null;
            context.InspectedControl = null;
            _assignmentsView.CommandBrowser.CommandsList.SelectedItem = null;
            _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "";
            context.Session = opened; context.Revision = saved.Revision; context.SourceDirectory = null;
            Remember(); ReloadChoices(); refresh(true);
        }

        internal void Remember()
        {
            if (context.Session is not null) context.Services.Remember(context.Services.Recent() with { BackendId = context.Backend.SimId, WorkspaceId = context.Session.Snapshot.Id, DisplayName = context.Session.Snapshot.Name });
        }

        internal void ReloadChoices()
        {
            context.Refreshing = true;
            try
            {
                ProfilePicker.ItemsSource = context.Services.Store.List().Where(x => x.BackendId == context.Backend.SimId).ToArray();
                ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == context.Session?.Snapshot.Id);
            }
            finally { context.Refreshing = false; }
        }

        internal void ImportTemplatesClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            var dialog = new OpenFileDialog { Title = "Import assignment templates from an original Squadrons capture", Filter = "Squadrons controls|ProfileOptions_profile_synced|All files|*.*" };
            if (dialog.ShowDialog(owner) == true) ImportTemplatesFile(dialog.FileName);
        });

        internal void ImportClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            var dialog = new OpenFileDialog { Title = "Import Squadrons controls", Filter = "Squadrons controls|ProfileOptions_profile_synced|All files|*.*" };
            if (dialog.ShowDialog(owner) == true) ImportFile(dialog.FileName);
        });

        internal void ExportClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder for a separate Squadrons review copy" };
            if (dialog.ShowDialog(owner) == true) ExportReview(dialog.FolderName);
        });

        internal void NewProfileClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (!NewProfileButton.IsEnabled) return;
            controllers().StopLiveInput();
            string? name;
            if (ConfigurationNamePicker is not null) name = ConfigurationNamePicker("New", "New Controls");
            else
            {
                var dialog = new GameConfigurationNameWindow(owner, "New", "New Controls");
                name = dialog.ShowDialog() == true ? dialog.ConfigurationName : null;
            }
            if (name is not null) NewProfile(name);
        });
        #endregion END private helpers
    }
}
