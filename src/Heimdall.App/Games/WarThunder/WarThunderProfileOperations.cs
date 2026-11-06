using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;
using Microsoft.Win32;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Owns bounded native import, optimistic persistence, duplication and separate review export.</summary>
    internal sealed class WarThunderProfileOperations(
        WarThunderWorkspaceContext context,
        Action refresh,
        Func<bool> confirmLeave,
        TextBlock Status,
        TextBox ProfileName,
        ComboBox Profiles,
        Window owner,
        EventHandler sessionChanged,
        Func<WarThunderControllerPresenter> controllers,
        Action refreshChrome)
    {
        #region BEGIN Public
        internal void SaveCore()
        {
            if (context.Session is not { } session || session.AccessMessage.Length != 0 || context.Testing() || context.ScanningHardware)
                throw new InvalidOperationException("This War Thunder workspace cannot currently be saved.");
            var workspace = session.Snapshot with { Name = context.EditedName };
            GameConfigurationName.Validate(workspace.Name);
            controllers().Input.Stop();
            var revision = context.Services.Store.Save(workspace, context.Revision);
            session.Rename(workspace.Name); session.AcceptSaved(); context.Revision = revision;
            Remember(); RefreshEntries(); refreshChrome(); Status.Text = "War Thunder profile saved.";
        }
        #endregion END Public
        #region BEGIN private helpers
        internal void ImportFile(string path)
        {
            controllers().Input.Stop();
            var full = Path.GetFullPath(path);
            if (new FileInfo(full).Length > 8 * 1024 * 1024) throw new InvalidDataException("Controls capture exceeds 8 MiB.");
            var profile = context.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(full)] = File.ReadAllText(full) });
            if (!confirmLeave()) return;
            Open(new(1, Guid.NewGuid(), Path.GetFileNameWithoutExtension(full), profile, new()) { SourceDirectory = Path.GetDirectoryName(full) }, null);
            Status.Text = "Imported captured commands. Save this profile, then export a separate review copy.";
        }

        internal void LoadWorkspace(Guid id)
        {
            if (context.Busy) throw new InvalidOperationException("Another workspace operation is still running.");
            var saved = context.Services.Store.Load(id);
            Open(saved.Workspace, saved.Revision);
        }

        internal string ExportReview(string parent)
        {
            if (context.Busy || context.Session is null || context.Chrome.Pending) throw new InvalidOperationException("Save the profile before exporting.");
            controllers().Input.Stop();
            var plan = context.Session.Export();
            return GameReviewExport.Write(parent, plan.Files,
                context.Session.Snapshot.SourceDirectory is { } source ? [source] : [], "war-thunder");
        }

        private void Open(GameWorkspace workspace, string? revision)
        {
            controllers().Input.Stop(); context.SelectedPhysicalInput = null; context.Selection.Reset();
            var session = new GameProfileSession(context.Backend, workspace, EditionAccess.Policy, context.Drawings.Select(item => item.DeviceId).ToArray(),
                () => context.Observed, new WarThunderDeviceReferenceMatcher(), controllerInputs: () => context.ControllerInputs);
            if (context.Session is not null) context.Session.Changed -= sessionChanged;
            context.Session = session; context.Revision = revision; context.Session.Changed += sessionChanged;
            context.Refreshing = true; ProfileName.Text = workspace.Name; context.Refreshing = false;
            RefreshEntries(); refresh(); Remember();
        }

        internal void RefreshEntries()
        {
            context.Refreshing = true;
            Profiles.ItemsSource = context.Services.Store.List().Where(item => item.BackendId == "war-thunder").ToArray();
            Profiles.SelectedItem = Profiles.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(item => item.Id == context.Session?.Snapshot.Id);
            context.Refreshing = false;
        }

        internal void DuplicateCurrent()
        {
            if (context.Session is null || !confirmLeave()) return;
            var dialog = new GameConfigurationNameWindow(owner, "Duplicate", context.Session.Snapshot.Name + " copy");
            if (dialog.ShowDialog() != true) return;
            var copy = GameConfigurations.Create(context.Session.Snapshot, dialog.ConfigurationName, false);
            Open(copy, null);
        }

        internal void Remember()
        {
            if (context.Session is not null && context.Revision is not null)
                context.Services.Remember(context.Services.Recent() with { BackendId = "war-thunder", WorkspaceId = context.Session.Snapshot.Id, DisplayName = context.Session.Snapshot.Name });
        }
        #endregion END private helpers
    }
}
