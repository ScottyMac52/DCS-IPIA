using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.App;
using Microsoft.Data.Sqlite;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises automatic setup, defer, confirmation and menu rerun in an isolated Windows profile.</summary>
    public static class SetupSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies real startup opens the shared review surface and completed setup does not reopen.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-setup-smoke-" + Guid.NewGuid().ToString("N"));
            var profile = UserDataProfile.Select(root, "fresh");
            Directory.CreateDirectory(profile.DataDirectory);
            try
            {
                var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(profile.DataDirectory, "ipia.db"), Pooling = false }.ToString();
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var inventory = new SqliteHardwareInventoryRepository(connection);
                var caps = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button")]);
                var discovery = new InventorySmokeDiscovery { Observations = [new(new(Guid.NewGuid(), "Unbundled panel", true), caps)] };
                var manager = new HardwareInventoryManager(inventory, discovery);
                var setup = new SetupManager(manager, new SqliteSetupRepository(connection));
                var lifecycle = new UserDataProfileService(profile, "1");
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "defaults.json"),
                    Path.Combine(profile.DataDirectory, "settings.json"), Path.Combine(root, "theme.txt"));
                MainWindow Main() => new(new ModuleWorkspaceLoader(), new DcsFolderImporter(), settings,
                    new SqliteEditorWorkspaceRepository(connection), profile: profile, profileService: lifecycle,
                    hardwareInventory: manager, setup: setup);

                var first = Main();
                Console.WriteLine("Setup smoke: automatic first-run skip.");
                DriveWizard(first, () => first.Show(), window => Click((Button)window.FindName("SetupSkip")!));
                if (!lifecycle.SetupOffered || lifecycle.SetupCompleted || inventory.ReadAsync().GetAwaiter().GetResult().Instances.Count != 0)
                    throw new InvalidOperationException("Skipping first-run setup adopted devices or marked completion.");
                first.Close();

                var second = Main();
                Console.WriteLine("Setup smoke: skipped setup stays dismissed after restart; explicit resume still works.");
                ShowWithoutSetup(second);
                var destination = Path.Combine(root, "Saved Games", "DCS");
                DriveWizard(second, () => MainWindowCommands.Setup.Execute(null, second), window =>
                {
                    var row = ((DataGrid)window.FindName("HardwareInventoryGrid")!).Items.Cast<HardwareInventoryRow>().Single();
                    row.Selected = true; row.Label = "My generic panel";
                    if (!row.MappingChoice.StartsWith("Generic", StringComparison.Ordinal))
                        throw new InvalidOperationException("Onboarding silently selected personal artwork.");
                    ((TextBox)window.FindName("SetupDestination")!).Text = destination;
                    Click((Button)window.FindName("HardwareInventorySave")!);
                });
                if (!lifecycle.SetupCompleted || settings.Current.SavedGamesDcsFolder != destination || Directory.Exists(destination))
                    throw new InvalidOperationException("Setup completion, destination refresh or read-only DCS behavior failed.");
                second.Close();

                var completed = Main();
                Console.WriteLine("Setup smoke: completed startup and menu rerun.");
                ShowWithoutSetup(completed);
                if (completed.OwnedWindows.Count != 0 || !MainWindowCommands.Setup.CanExecute(null, completed))
                    throw new InvalidOperationException("Completed startup reopened setup or disabled menu rerun.");
                DriveWizard(completed, () => MainWindowCommands.Setup.Execute(null, completed), window =>
                {
                    var row = ((DataGrid)window.FindName("HardwareInventoryGrid")!).Items.Cast<HardwareInventoryRow>().Single();
                    if (!row.Selected || row.Label != "My generic panel") throw new InvalidOperationException("Rerun discarded confirmed hardware.");
                    Click((Button)window.FindName("SetupSkip")!);
                });
                if (!lifecycle.SetupCompleted || inventory.ReadAsync().GetAwaiter().GetResult().Instances.Count != 1)
                    throw new InvalidOperationException("Deferred rerun changed inventory or cleared completion.");
                completed.Close();
                var restarted = Main();
                ShowWithoutSetup(restarted);
                var profiles = new UserDataProfileWindow(profile, lifecycle, () => { }) { Owner = restarted };
                profiles.Show();
                UserGuideCapture.Save(profiles, "test-profiles");
                Click(ActionBar.FindButton(profiles, "Rerun setup (preserve saved data)")!);
                if (!profiles.ResumeSetupRequested || !lifecycle.SetupCompleted)
                    throw new InvalidOperationException("Profiles/reset rerun cleared completion before setup opened.");
                DriveWizard(restarted, () => MainWindowCommands.Setup.Execute(null, restarted), window => window.Close());
                if (!lifecycle.SetupCompleted) throw new InvalidOperationException("Closing a rerun cleared completion.");
                restarted.Close();
                discovery.Observations = [];
                Console.WriteLine("Setup smoke: explicit offline confirmation.");
                using var offline = new HardwareInventoryWindow(manager, true,
                    _ => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>()), setup);
                offline.Show();
                Pump(offline, () => ((Button)offline.FindName("HardwareInventorySave")!).IsEnabled);
                ((CheckBox)offline.FindName("SetupOffline")!).IsChecked = true;
                Click((Button)offline.FindName("HardwareInventorySave")!);
                Pump(offline, () => offline.SetupConfirmed);
                if (!lifecycle.SetupCompleted) throw new InvalidOperationException("Explicit offline setup did not finish.");
                Console.WriteLine("First-run setup deferred, confirmed generic hardware, survived startup and reran without deploying DCS files.");
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ShowWithoutSetup(MainWindow main)
        {
            var unexpected = false;
            var guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            guard.Tick += (_, _) =>
            {
                foreach (var wizard in main.OwnedWindows.OfType<HardwareInventoryWindow>().ToArray())
                {
                    unexpected = true;
                    wizard.Close();
                }
            };
            guard.Start();
            try
            {
                main.Show();
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                if (unexpected || main.OwnedWindows.Count != 0)
                    throw new InvalidOperationException("Previously offered setup reopened automatically.");
            }
            finally { guard.Stop(); }
        }
        private static void DriveWizard(MainWindow main, Action open, Action<HardwareInventoryWindow> review)
        {
            var handled = false;
            Exception? failure = null;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                var window = main.OwnedWindows.OfType<HardwareInventoryWindow>().SingleOrDefault();
                if (DateTime.UtcNow > deadline)
                {
                    failure = new InvalidOperationException("Automatic setup timed out: " + ((TextBlock)main.FindName("FlyNowStatus")!).Text);
                    timer.Stop(); window?.Close(); return;
                }
                if (window is null) return;
                if (!((Button)window.FindName("HardwareInventorySave")!).IsEnabled) return;
                Console.WriteLine("Setup smoke: review surface ready.");
                timer.Stop();
                try { review(window); handled = true; }
                catch (Exception ex) { failure = ex; window.Close(); }
            };
            timer.Start();
            try { open(); Pump(main, () => handled || failure is not null); }
            finally { timer.Stop(); }
            if (failure is not null) throw new InvalidOperationException("Setup review failed.", failure);
        }
        private static void Click(Button button)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Pump(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready())
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Setup UI timed out.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
