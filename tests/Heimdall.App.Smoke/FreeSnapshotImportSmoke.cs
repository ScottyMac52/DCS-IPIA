using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Runs actual Free snapshot restore commands and confirmation buttons against isolated SQLite stores.</summary>
    public static class FreeSnapshotImportSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies Cancel, close, supported-only acceptance, compatible bypass and an all-unsupported abort.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-free-snapshot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy;
            try
            {
                EditionAccess.SetForTests(new EditionPolicy());
                var workspace = MixedSnapshotFixture.Create();
                var original = ModuleSnapshotWorkflow.Freeze(workspace, "1.2.3");
                Run(root, "cancel", original, 1);
                Run(root, "close", original, 1);
                Run(root, "accept", original, 1);
                Run(root, "compatible", ModuleSnapshotEditionFilter.KeepSupported(original, new()), 0);
                var none = workspace with { Configuration = new(workspace.Configuration.Module,
                    workspace.Configuration.Profiles.Select(p => new DeviceProfile(p.Id, "reserved-" + p.DeviceId, p.Instance, p.Role, p.SourceName)),
                    workspace.Configuration.Modifiers, workspace.Configuration.Bindings, workspace.Configuration.Provenance) };
                Run(root, "none", ModuleSnapshotWorkflow.Freeze(none, "1.2.3"), 2);
            }
            finally
            {
                EditionAccess.SetForTests(previous); AppearanceManager.Restore(); ThemeManager.Apply("Light");
                Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Run(string root, string action, ModuleBuildSnapshot snapshot, int unsupportedCount)
        {
            var folder = Path.Combine(root, action); Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "module.heimdall-module.json"); var json = ModuleSnapshotWorkflow.Export(snapshot); File.WriteAllText(file, json);
            var connection = "Data Source=" + Path.Combine(folder, "state.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new SqliteEditorWorkspaceRepository(connection);
            var settings = new FileAppSettingsService(Path.Combine(folder, "defaults.json"), Path.Combine(folder, "settings.json"), Path.Combine(folder, "theme.txt"));
            var main = new MainWindow(new ModuleWorkspaceLoader(), new DcsFolderImporter(), settings, repository,
                snapshotFilePicker: () => file, deviceDiscovery: new EmptyDeviceDiscovery());
            var seen = false; string? promptError = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Name == "SnapshotImportReviewDialog");
                if (dialog is null) return;
                seen = true; timer.Stop();
                var children = ThemeControlSmoke.Children(dialog).ToArray();
                var list = children.OfType<ListBox>().Single(c => c.Name == "UnsupportedSnapshotDevices");
                var accept = children.OfType<Button>().Single(b => b.Name == "ImportSupportedDevicesButton");
                var cancel = children.OfType<Button>().Single(b => b.Name == "CancelSnapshotImportButton");
                if (list.Items.Count != unsupportedCount || !cancel.IsDefault || accept.IsEnabled != (action != "none"))
                    promptError = "Snapshot review did not identify unsupported devices, safe default or all-unsupported blocking.";
                ThemeControlSmoke.SaveImage(dialog, "free-snapshot-review-" + action);
                if (action == "close") dialog.Close();
                else (action == "accept" && promptError is null ? accept : cancel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            try
            {
                main.Show(); timer.Start();
                var previous = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(main.Dispatcher));
                    ((Button)main.FindName("RestoreModuleSnapshotButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                var status = (TextBlock)main.FindName("ImportStatus")!; var deadline = DateTime.UtcNow.AddSeconds(60);
                while (!status.Text.StartsWith("Restored ", StringComparison.Ordinal) &&
                    !status.Text.StartsWith("Module snapshot import cancelled", StringComparison.Ordinal) &&
                    !status.Text.StartsWith("Module snapshot was not restored:", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Snapshot import timed out: " + action + ": " + status.Text);
                    main.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                if (promptError is not null) throw new InvalidOperationException(promptError);
                if (seen != (unsupportedCount > 0)) throw new InvalidOperationException("Snapshot import confirmation was missing or unnecessary.");
                var imported = repository.ListAsync().GetAwaiter().GetResult();
                var accepted = action is "accept" or "compatible";
                if (imported.Count != (accepted ? 1 : 0) || accepted != status.Text.StartsWith("Restored ", StringComparison.Ordinal))
                    throw new InvalidOperationException("Snapshot import wrote unexpected module data: " + action + ": " + status.Text);
                if (accepted)
                {
                    var saved = repository.GetAsync(imported[0]).GetAwaiter().GetResult()!;
                    if (!EditionAccess.AllowsWorkspace(saved) || saved.Configuration.Profiles.Count != 1 || saved.Assignments.Count != 3)
                        throw new InvalidOperationException("Accepted snapshot remained edition-locked or lost supported assignments.");
                    var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), settings, repository, new EmptyDeviceDiscovery());
                    try
                    {
                        editor.Show(); ThemeControlSmoke.Pump(editor);
                        if (!((ContentControl)editor.FindName("FeatureContent")!).IsEnabled || !((FeatureActionBar)editor.FindName("FeatureActions")!).IsEnabled)
                            throw new InvalidOperationException("Supported-only imported module still opened read-only.");
                    }
                    finally { editor.Close(); }
                }
                if (File.ReadAllText(file) != json) throw new InvalidOperationException("Import modified the source snapshot.");
            }
            finally { timer.Stop(); main.Close(); }
        }
        #endregion END private helpers
    }
}
