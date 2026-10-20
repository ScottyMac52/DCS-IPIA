using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.App;
using Microsoft.Data.Sqlite;

namespace Heimdall.App.Smoke
{
    /// <summary>Reproduces Free setup with selected saved Thrustmaster units and no current scan evidence.</summary>
    public static class FreeInventorySetupSmoke
    {
        private static readonly string[] Names = ["Viper TQS", "T-Pendular-Rudder", "F16 MFD 1", "F16 MFD 2", "F16 MFD 3", "Reserved panel"];
        #region BEGIN Public
        /// <summary>Checks real SQLite history, inventory save and setup confirmation while preserving reserved records.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-free-setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy;
            var appearance = AppearanceManager.Store;
            try
            {
                EditionAccess.SetForTests(new EditionPolicy());
                AppearanceManager.Configure(null);
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteHardwareInventoryRepository(connection);
                var tm = Guid.Parse("0402044f-0000-0000-0000-504944564944");
                var other = Guid.Parse("0402046d-0000-0000-0000-504944564944");
                var caps = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button")]);
                var discovery = new InventorySmokeDiscovery { Observations = Names
                    .Select((name, index) => new ControllerObservation(new(Guid.NewGuid(), name, true, ProductGuid: index == 5 ? other : tm), caps)).ToArray() };
                var manager = new HardwareInventoryManager(repository, discovery);
                // Establish the same saved rows and historical evidence as an earlier successful scan/adoption.
                var scanned = manager.ScanAsync().GetAwaiter().GetResult();
                manager.SaveAsync(scanned.Select(row => new HardwareInventoryEdit(row, true,
                    row.Observed!.Device.Name, "", false, [])).ToArray()).GetAwaiter().GetResult();
                scanned = manager.ScanAsync().GetAwaiter().GetResult();
                manager.SaveAsync(scanned.Select(row => new HardwareInventoryEdit(row, row.Observed!.Device.ProductGuid == tm,
                    row.Observed.Device.Name, "", false, [])).ToArray()).GetAwaiter().GetResult();
                discovery.Observations = [];
                var stored = repository.ReadAsync().GetAwaiter().GetResult();
                if (stored.Observations.Any(x => x.Device.Connected)) throw new InvalidOperationException("History was incorrectly marked connected.");

                using (var inventory = new HardwareInventoryWindow(manager, false))
                {
                    inventory.Show(); Ready(inventory);
                    var rows = ((DataGrid)inventory.FindName("HardwareInventoryGrid")).Items.Cast<HardwareInventoryRow>().ToArray();
                    if (rows.Count(x => x.Selected) != 5 || rows.Any(x => x.Candidate.Observed is not null))
                        throw new InvalidOperationException("Free setup fixture does not reproduce the reported offline selections.");
                    Click(inventory, "HardwareInventorySave");
                    Wait(inventory, () => Status(inventory).StartsWith("Reviewed inventory saved", StringComparison.Ordinal));
                    if (repository.ReadAsync().GetAwaiter().GetResult().Instances.Count != 6)
                        throw new InvalidOperationException("Free inventory save lost verified offline units.");
                    // A current scan identifying a previously verified unit as another vendor must still be blocked.
                    var current = stored.Observations.First(x => x.Device.ProductGuid == tm);
                    discovery.Observations = [current with { Device = current.Device with { Connected = true, ProductGuid = other } }];
                    Click(inventory, "HardwareInventoryRescan"); Ready(inventory);
                    Click(inventory, "HardwareInventorySave");
                    Wait(inventory, () => Status(inventory).StartsWith("Inventory was not saved", StringComparison.Ordinal));
                    if (!Status(inventory).Contains(current.Device.Name, StringComparison.Ordinal) || !Status(inventory).Contains("Rescan", StringComparison.Ordinal))
                        throw new InvalidOperationException("Blocked controller was not identified with recovery guidance.");
                    ThemeControlSmoke.SaveImage(inventory, "free-inventory-verification-error");
                    inventory.Close();
                }
                discovery.Observations = [];
                var setup = new SetupManager(manager, new SqliteSetupRepository(connection));
                using var wizard = new HardwareInventoryWindow(manager, true,
                    _ => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>()), setup);
                wizard.Show(); Ready(wizard);
                ((TextBox)wizard.FindName("SetupDestination")).Text = Path.Combine(root, "Saved Games", "DCS");
                ThemeControlSmoke.SaveImage(wizard, "free-offline-thrustmaster-setup");
                Click(wizard, "HardwareInventorySave");
                Wait(wizard, () => wizard.SetupConfirmed);
                if (!setup.ReadAsync().GetAwaiter().GetResult().Completed || repository.ReadAsync().GetAwaiter().GetResult().Instances.Count(x => x.Selected) != 5)
                    throw new InvalidOperationException("Free setup did not commit the selected offline Thrustmaster inventory.");
            }
            finally
            {
                EditionAccess.SetForTests(previous);
                AppearanceManager.Configure(appearance);
                SqliteConnection.ClearAllPools(); Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Status(Window window) => ((TextBlock)window.FindName("HardwareInventoryStatus")).Text;
        private static void Click(Window window, string name)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
            try { ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Ready(Window window) => Wait(window, () => ((Button)window.FindName("HardwareInventorySave")).IsEnabled &&
            ((DataGrid)window.FindName("HardwareInventoryGrid")).Items.Count > 0);
        private static void Wait(Window window, Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Free hardware setup timed out: " + Status(window));
                ThemeControlSmoke.Pump(window);
                Thread.Sleep(10);
            }
        }
        #endregion END private helpers
    }
}
