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
    /// <summary>Checks native enumeration and main-window inventory adoption with no open module.</summary>
    public static class HardwareInventorySmoke
    {
        #region BEGIN Public
        /// <summary>Verifies repeated units, explicit save and disconnected reopening in the WPF surface.</summary>
        public static void Verify()
        {
            var native = new WindowsConnectedDeviceDiscovery().DiscoverAsync().GetAwaiter().GetResult();
            if (native.Select(x => x.Device.PhysicalGuid).Distinct().Count() != native.Count)
                throw new InvalidOperationException("Native discovery collapsed or duplicated physical units.");
            foreach (var observed in native.Where(x => x.Capabilities is not null))
            {
                var caps = observed.Capabilities!;
                if (caps.DcsKeys.Count != caps.Inputs.Sum(x => x.Kind == ControllerInputKind.Hat ? 8 : 1))
                    throw new InvalidOperationException("Native input counts disagree with live-test control identities.");
            }
            var folder = Path.Combine(Path.GetTempPath(),"ipia-inventory-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder,"ipia.db"), Pooling = false }.ToString();
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteHardwareInventoryRepository(connection);
                var caps = ControllerCapabilityMapper.Map([new(Guid.NewGuid(),12,"Button")]);
                var fake = new InventorySmokeDiscovery
                {
                    Observations = [new(new(Guid.NewGuid(),"Identical panel",true),caps),
                        new(new(Guid.NewGuid(),"Identical panel",true),caps)]
                };
                var manager = new HardwareInventoryManager(repository,fake);
                var settings = new SqliteAppSettingsService(connection,Path.Combine(folder,"defaults.json"),
                    Path.Combine(folder,"settings.json"),Path.Combine(folder,"theme.txt"));
                var main = new MainWindow(new ModuleWorkspaceLoader(),new DcsFolderImporter(),settings,
                    new SqliteEditorWorkspaceRepository(connection),hardwareInventory:manager);
                main.Show();
                if (!MainWindowCommands.DiscoverHardware.CanExecute(null,main) ||
                    !MainWindowCommands.ManageHardware.CanExecute(null,main))
                    throw new InvalidOperationException("Hardware commands require an open module.");
                main.Close();
                using var window = new HardwareInventoryWindow(manager,true,_ => Task.FromResult<IReadOnlyDictionary<string,string>>(new Dictionary<string,string>()));
                window.Show();
                var grid = (DataGrid)window.FindName("HardwareInventoryGrid")!;
                var save = (Button)window.FindName("HardwareInventorySave")!;
                Pump(window,() => grid.Items.Count == 2 && save.IsEnabled);
                var rows = grid.Items.Cast<HardwareInventoryRow>().ToArray();
                if (rows.Any(x => x.Selected) || rows[0].Identity == rows[1].Identity)
                    throw new InvalidOperationException("New devices were selected automatically or repeated identities collapsed.");
                rows[0].Selected = true; rows[0].Label = "Left panel";
                rows[1].Selected = true; rows[1].Label = "Right panel";
                UserGuideCapture.Save(window, "hardware-inventory");
                Click(save);
                Pump(window,() => ((TextBlock)window.FindName("HardwareInventoryStatus")!).Text.StartsWith("Reviewed inventory saved",StringComparison.Ordinal));
                window.Close();
                using var reopened = new HardwareInventoryWindow(manager,false,_ => Task.FromResult<IReadOnlyDictionary<string,string>>(new Dictionary<string,string>()));
                reopened.Show();
                var saved = (DataGrid)reopened.FindName("HardwareInventoryGrid")!;
                Pump(reopened,() => saved.Items.Count == 2 && ((Button)reopened.FindName("HardwareInventorySave")!).IsEnabled);
                if (saved.Items.Cast<HardwareInventoryRow>().Any(x => !x.Selected || !x.Status.StartsWith("Disconnected",StringComparison.Ordinal)) ||
                    saved.Items.Cast<HardwareInventoryRow>().Select(x => x.Label).Distinct().Count() != 2)
                    throw new InvalidOperationException("Saved inventory did not preserve selection, labels and offline units.");
                reopened.Close();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(folder,true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Button button)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Pump(Window window,Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready())
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Hardware inventory UI timed out.");
                window.Dispatcher.Invoke(() => { },DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
