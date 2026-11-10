using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks rejected device additions and inventory membership through the real WPF and SQLite paths.</summary>
    internal static class DcsDeviceAddSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies truthful add feedback and a selected inventory unit surviving save and reopen.</summary>
        internal static void Verify()
        {
            var previous = EditionAccess.Policy;
            using var issuer = new OwnerLicenseTestIssuer();
            EditionAccess.SetForTests(issuer.PremiumPolicy);
            var path = Path.Combine(Path.GetTempPath(), "heimdall-device-add-" + Guid.NewGuid().ToString("N") + ".db");
            DcsModuleWorkspaceWindow? window = null;
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new DcsSqliteEditorWorkspaceRepository(connection);
                var capabilities = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button")]);
                var discovery = new InventorySmokeDiscovery
                {
                    Observations = [new(new(Guid.NewGuid(), "SOL-R test unit", true), capabilities)]
                };
                var inventory = new HardwareInventoryManager(new SqliteHardwareInventoryRepository(connection), discovery);
                var scanned = inventory.ScanAsync().GetAwaiter().GetResult();
                inventory.SaveAsync(scanned.Select(row => new HardwareInventoryEdit(row, true, "SOL-R test unit", "", false, [])).ToArray()).GetAwaiter().GetResult();
                var workspace = MixedSnapshotFixture.Create();
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                window = new(new DcsModuleWorkspaceViewModel(workspace), repository: repository,
                    deviceDiscovery: new EmptyDeviceDiscovery(), inventory: inventory, persistLayout: false);
                window.Show();
                var view = WorkspaceUi.Navigate(window, "Devices");
                var choices = (ComboBox)view.FindName("InventoryUnits");
                Wait(window, () => choices.Items.Count == 1);
                choices.SelectedIndex = 0;
                var choice = (InventoryHardwareChoice)choices.SelectedItem;
                var devices = (DataGrid)view.FindName("ManagedDevices");
                var before = JsonSerializer.Serialize(window.DcsContext.Working);
                var count = devices.Items.Count;
                var hardware = (ComboBox)view.FindName("ManagedHardware");
                hardware.ItemsSource = new[] { GenericHardwareLayout.Create(choice.Definition) };
                hardware.SelectedIndex = 0;
                ((TextBox)view.FindName("ManagedFileName")).Text = "";
                ((Button)view.FindName("AddSharedHardwareButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var status = (TextBlock)view.FindName("StatusMessage");
                Require(status.Text.Contains("filename", StringComparison.OrdinalIgnoreCase) &&
                    !status.Text.Contains("added and staged", StringComparison.OrdinalIgnoreCase), "A rejected filename reported a successful device addition.");
                Require(JsonSerializer.Serialize(window.DcsContext.Working) == before && devices.Items.Count == count &&
                    !window.Session.HasPendingChanges, "A rejected device addition changed the workspace.");
                Require(((Expander)view.FindName("DeviceSettings")).IsExpanded, "Rejected native settings remained hidden.");
                ((Button)view.FindName("AddInventoryUnitButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(devices.Items.Count == count + 1 && devices.SelectedItem is DcsWorkspaceDevice added &&
                    added.PhysicalGuid == choice.Instance.PhysicalGuid && added.FileName == choice.FileName,
                    "The inventory addition was not visible and selected in the module list.");
                Require(window.Session.HasPendingChanges && status.Text.StartsWith("Inventory unit added and staged", StringComparison.Ordinal),
                    "The valid inventory addition did not report a staged change.");
                var staged = JsonSerializer.Serialize(window.DcsContext.Working);
                ((Button)view.FindName("AddInventoryUnitButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(status.Text.Contains("already belongs", StringComparison.Ordinal) &&
                    JsonSerializer.Serialize(window.DcsContext.Working) == staged && devices.Items.Count == count + 1,
                    "Duplicate inventory addition reported success or changed the draft.");
                var save = StartSave(window);
                Wait(window, () => save.IsCompleted);
                save.GetAwaiter().GetResult();
                Require(!window.Session.HasPendingChanges, "Saving the added device left pending changes.");
                window.Close(); window = null;
                var persisted = repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!;
                window = new(new DcsModuleWorkspaceViewModel(persisted), repository: repository,
                    deviceDiscovery: new EmptyDeviceDiscovery(), inventory: inventory, persistLayout: false);
                window.Show();
                var reopened = WorkspaceUi.Navigate(window, "Devices");
                var memberships = ((DataGrid)reopened.FindName("ManagedDevices")).Items.Cast<DcsWorkspaceDevice>().ToArray();
                Require(memberships.Length == count + 1 && memberships.Any(x => x.PhysicalGuid == choice.Instance.PhysicalGuid),
                    "The saved inventory device disappeared after reopening from SQLite.");
            }
            finally
            {
                if (window is not null) { window.DcsContext.Discard(); window.Close(); }
                EditionAccess.SetForTests(previous);
                File.Delete(path);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Task<Heimdall.Application.Games.WorkspaceOperationResult> StartSave(DcsModuleWorkspaceWindow window)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
            try { return window.SaveWorkspaceAsync(); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Wait(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!ready())
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Device-add UI or persistence timed out.");
                ThemeControlSmoke.Pump(window);
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        #endregion END private helpers
    }
}
