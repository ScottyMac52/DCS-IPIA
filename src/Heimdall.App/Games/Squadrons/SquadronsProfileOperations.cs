using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Domain.MultiSim;
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
        internal void SaveCore()
        {
            if (context.Session is not { } session || session.AccessMessage.Length > 0)
                throw new InvalidOperationException("This Squadrons workspace cannot currently be saved.");
            var workspace = session.Snapshot with { Name = context.EditedName };
            GameConfigurationName.Validate(workspace.Name);
            var revision = context.Services.Store.Save(workspace, context.Revision);
            session.Rename(workspace.Name); session.AcceptSaved(); context.Revision = revision;
            Remember(); ReloadChoices(); refresh(true);
            _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "";
            StatusText.Text = "Squadrons profile saved.";
        }
        #endregion END Public
        #region BEGIN private helpers
        internal void ImportFile(string path)
        {
            if (!confirmLeave()) return;
            controllers().StopLiveInput();
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Squadrons controls exceed the 4 MiB import limit.");
            var profile = context.Backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(path) });
            var name = "Squadrons Controls"; var number = 2; var existing = context.Services.Store.List().Where(x => x.BackendId == context.Backend.SimId).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (existing.Contains(name)) name = "Squadrons Controls " + number++;
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name, profile, new());
            var opened = CreateSession(workspace);
            context.Selection.Reset();
            context.FocusedConcept = null;
            context.InspectedControl = null;
            _assignmentsView.CommandBrowser.CommandsList.SelectedItem = null;
            _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "";
            context.Session = opened; context.Revision = null; context.SourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
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
            var sourceSession = context.Session;
            if (!confirmLeave()) return false;
            controllers().StopLiveInput();
            sourceSession.Export(); // Require the existing session's edition and native validation boundary.
            var source = sourceSession.Snapshot;
            var workspace = GameConfigurations.Create(source, name, false) with
            {
                Profile = context.Backend.CreateUnassignedProfile(source.Profile)
            };
            var sourceDirectory = context.SourceDirectory;
            context.Services.Store.Save(workspace, null);
            LoadProfile(workspace.Id);
            context.SourceDirectory = sourceDirectory;
            StatusText.Text = "New profile saved with no joystick assignments or imported assignment templates. Controller setup and native game settings retained.";
            return true;
        }

        internal string ExportReview(string parent)
        {
            if (context.Busy || context.Session is null || context.Chrome.Pending) throw new InvalidOperationException("Save the Squadrons profile before exporting.");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var roots = new List<string> { Path.Combine(documents, "STAR WARS Squadrons", "settings"), Path.Combine(documents, "STAR WARS Squadrons Steam", "settings") };
            if (context.SourceDirectory is not null) roots.Add(context.SourceDirectory);
            context.Session.Export(); // Retain the session's edition and native-document validation boundary.
            var snapshot = context.Session.Snapshot;
            var plan = context.Backend.ExportReview(snapshot.Profile, snapshot.Hardware.ToDictionary(x => x.Key, x => x.Value.Device.Name));
            if (!plan.Succeeded) throw new InvalidOperationException(string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            var directory = GameReviewExport.Write(parent, plan.Files, roots, backendId: context.Backend.SimId);
            StatusText.Text = "Review copy with preserved native names and records exported to " + directory + ". Close Squadrons and back up its settings before manually installing this copy.";
            return directory;
        }

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
