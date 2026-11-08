using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Games.Profiles;
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
        /// <summary>Native post-commit refresh for the shared save sequence; persistence itself is owned by <see cref="WarThunderProfileSaving"/>.</summary>
        internal void AfterSave() { Remember(); RefreshEntries(); refreshChrome(); Status.Text = "War Thunder profile saved."; }
        #endregion END Public
        #region BEGIN private helpers
        private WarThunderImportCommand? _import;
        private WarThunderExportCommand? _export;
        private WarThunderDuplicateCommand? _duplicate;
        private IWorkspaceInputLifetime Input => new DelegateInputLifetime(() => controllers().Input.Stop());
        private WarThunderImportCommand Import => _import ??= new(context, Input, CreateSession, Install, message => Status.Text = message);
        private WarThunderExportCommand Export => _export ??= new(context, Input, message => Status.Text = message);
        private WarThunderDuplicateCommand Duplicate => _duplicate ??= new(context, Input, CreateSession, Install);
        internal void ImportFile(string path)
        {
            controllers().Input.Stop();
            var request = WarThunderImportRequest.Read(context.Backend, path);
            if (!confirmLeave()) return;
            Import.ExecuteAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }

        internal void LoadWorkspace(Guid id)
        {
            if (context.Busy) throw new InvalidOperationException("Another workspace operation is still running.");
            var saved = context.Services.Store.Load(id);
            Open(saved.Workspace, saved.Revision);
        }

        internal string ExportReview(string parent) => Export.ExecuteAsync(parent, CancellationToken.None).GetAwaiter().GetResult().Native.Detail;

        private void Open(GameWorkspace workspace, string? revision) => Install(new(CreateSession(workspace), revision));

        private GameProfileSession CreateSession(GameWorkspace workspace) => new(context.Backend, workspace, EditionAccess.Policy, context.Drawings.Select(item => item.DeviceId).ToArray(),
            () => context.Observed, new WarThunderDeviceReferenceMatcher(), controllerInputs: () => context.ControllerInputs);

        private void Install(GameSessionInstall installed)
        {
            controllers().Input.Stop(); context.SelectedPhysicalInput = null; context.Selection.Reset();
            var workspace = installed.Session.Snapshot;
            if (context.Session is not null) context.Session.Changed -= sessionChanged;
            context.Session = installed.Session; context.Revision = installed.Revision; context.Session.Changed += sessionChanged;
            context.Refreshing = true; ProfileName.Text = workspace.Name; context.Refreshing = false;
            RefreshEntries(); refresh(); Remember();
        }

        internal void RefreshEntries()
        {
            context.Refreshing = true;
            Profiles.ItemsSource = context.Services.Store.List().Where(item => item.BackendId == "war-thunder").ToArray();
            Profiles.SelectedItem = Profiles.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(item => item.Id == context.Session?.Id);
            context.Refreshing = false;
        }

        internal void DuplicateCurrent()
        {
            if (context.Session is null || !confirmLeave()) return;
            var dialog = new GameConfigurationNameWindow(owner, "Duplicate", context.Session.Name + " copy");
            if (dialog.ShowDialog() != true) return;
            Duplicate.ExecuteAsync(dialog.ConfigurationName, CancellationToken.None).GetAwaiter().GetResult();
        }

        internal void Remember()
        {
            if (context.Session is not null && context.Revision is not null)
                context.Services.Remember(context.Services.Recent() with { BackendId = "war-thunder", WorkspaceId = context.Session.Id, DisplayName = context.Session.Name });
        }
        #endregion END private helpers
    }
}
