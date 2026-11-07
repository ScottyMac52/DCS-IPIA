using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks real DCS editing paths during delayed successful and failed database transactions.</summary>
    internal static class DcsWorkspaceSaveSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var previous = EditionAccess.Policy;
            using var issuer = new OwnerLicenseTestIssuer();
            EditionAccess.SetForTests(issuer.PremiumPolicy);
            var path = Path.Combine(Path.GetTempPath(), "heimdall-save-" + Guid.NewGuid().ToString("N") + ".db");
            DcsModuleWorkspaceWindow? window = null;
            PausedDcsRepository? repository = null;
            Task? save = null;
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var inner = new DcsSqliteEditorWorkspaceRepository(connection);
                var workspace = MixedSnapshotFixture.Create();
                inner.SaveAsync(workspace).GetAwaiter().GetResult();
                repository = new PausedDcsRepository(inner);
                window = new(new DcsModuleWorkspaceViewModel(workspace), repository: repository, deviceDiscovery: new EmptyDeviceDiscovery(), persistLayout: false);
                window.Show();
                var context = window.DcsContext;
                var draft = context.Draft!;
                var removed = draft.Snapshot[0].Slot;
                draft.Remove(removed);
                var pending = JsonSerializer.Serialize(draft.Snapshot);
                repository.Pause(fail: true);
                save = StartSave(window);
                Require(window.Session.IsSaving && !save.IsCompleted, "The save did not enter its asynchronous interval.");
                var assignments = WorkspaceUi.Navigate(window, "Assignments");
                Require(!((ContentControl)window.FindName("FeatureContent")).IsEnabled &&
                    !((FeatureActionBar)window.FindName("FeatureActions")).IsEnabled, "Navigation re-enabled editing during save.");
                Reject(() => context.SaveAsync().GetAwaiter().GetResult());
                Reject(context.Discard);
                ((Button)assignments.FindName("SaveDraftButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var feedback = (TextBlock)assignments.FindName("SelectedCallout");
                Require(feedback.Text.StartsWith("Database save failed:", StringComparison.Ordinal), "Assignments did not report the blocked save. Actual status: " + feedback.Text);
                ((Button)assignments.FindName("DiscardDraftButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(feedback.Text.StartsWith("Discard failed:", StringComparison.Ordinal), "Assignments did not report the blocked discard. Actual status: " + feedback.Text);
                Reject(() => context.SetLabel("during-save", "must not be applied"));
                var edited = false;
                context.RunStructureEdit(current => { edited = true; return current; });
                Require(!edited, "A structural edit reached the workspace during save.");
                Require(draft.Restore(removed).Outcome == DraftOutcome.Blocked, "A stale draft reference remained writable.");
                Require(repository.Writes == 1 && JsonSerializer.Serialize(draft.Snapshot) == pending, "A rejected operation changed the pending snapshot.");
                repository.Release();
                Pump(window, save);
                try { save.GetAwaiter().GetResult(); throw new InvalidOperationException("The failed repository save completed successfully."); }
                catch (IOException) { }
                Require(!window.Session.IsSaving && window.Session.HasPendingChanges &&
                    JsonSerializer.Serialize(context.Draft!.Snapshot) == pending, "Save failure lost pending edits or retained the lock.");
                Require(draft.Restore(removed).Outcome == DraftOutcome.Applied, "Failure did not release the original draft lease.");
                draft.Undo();
                repository.Pause(fail: false);
                save = StartSave(window);
                repository.Release(); Pump(window, save); save.GetAwaiter().GetResult();
                var persisted = inner.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!;
                Require(!window.Session.HasPendingChanges && !window.Session.IsSaving &&
                    JsonSerializer.Serialize(persisted.Assignments) == pending, "Save did not persist the unchanged reviewed draft.");
                context.SetLabel("after-save", "editing resumed");
                Require(window.Session.HasPendingChanges, "Editing did not resume after successful save.");
                context.Discard();
            }
            finally
            {
                if (window is not null)
                {
                    if (save is { IsCompleted: false }) { repository?.Release(); Pump(window, save); }
                    if (save?.IsFaulted == true) _ = save.Exception;
                    window.DcsContext.Discard(); window.Close();
                }
                EditionAccess.SetForTests(previous);
                File.Delete(path);
            }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static Task StartSave(DcsModuleWorkspaceWindow window)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
            try { return window.SaveWorkspaceAsync(); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        private static void Reject(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("A blocked persistence/edit operation reported success.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Pump(Window window, Task operation)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!operation.IsCompleted)
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Delayed save timed out.");
                ThemeControlSmoke.Pump(window);
            }
        }
        #endregion END private helpers
    }
}
