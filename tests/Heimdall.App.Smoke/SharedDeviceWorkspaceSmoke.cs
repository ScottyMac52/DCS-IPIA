using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.App.Games;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Squadrons;
using Heimdall.App.Games.WarThunder;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that all four real game windows expose one shared device manager without editing saved game data.</summary>
    internal static class SharedDeviceWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Exercises central device access, reconnect availability and DCS offline assignment gates.</summary>
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "heimdall-shared-device-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy;
            using var issuer = new OwnerLicenseTestIssuer();
            var windows = new List<Window>();
            try
            {
                EditionAccess.SetForTests(issuer.PremiumPolicy);
                var connection = "Data Source=" + Path.Combine(root, "inventory.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var observation = new ControllerObservation(new(MixedSnapshotFixture.Supported, "Shared panel", true),
                    ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button")]));
                var native = new InventorySmokeDiscovery { Observations = [observation] };
                var inventory = new HardwareInventoryManager(new SqliteHardwareInventoryRepository(connection), native);
                var row = inventory.ScanAsync().GetAwaiter().GetResult().Single();
                inventory.SaveAsync([new(row, true, "My shared panel", "Pilot", false, ["shared-drawing"], true)]).GetAwaiter().GetResult();
                var source = new InventoryDeviceDiscovery(inventory, native);
                var store = new FileGameWorkspaceStore(root);
                var services = new GameWorkspaceServices(store, source);
                if (!ReferenceEquals(inventory, services.Inventory) || services.PreferredDrawing(observation.Device.PhysicalGuid) != "shared-drawing")
                    throw new InvalidOperationException("Games did not consume the shared device inventory and drawing preference.");
                var workspace = MixedSnapshotFixture.Create();
                var before = DcsEditorSnapshotSerializer.Serialize(workspace);
                var dcs = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(workspace),
                    deviceDiscovery: source, inventory: inventory, persistLayout: false);
                windows.Add(dcs);
                windows.Add(new EliteWorkspaceWindow(services, []));
                windows.Add(new SquadronsWorkspaceWindow(services, drawings: []));
                windows.Add(new WarThunderWorkspaceWindow(services, drawings: []));
                foreach (var window in windows)
                {
                    window.Show(); ThemeControlSmoke.Pump(window);
                    if (!ThemeControlSmoke.Children(window).OfType<Button>().Any(button =>
                        AutomationProperties.GetName(button) == "Devices for all games"))
                        throw new InvalidOperationException("A game window has no entry point to central Devices: " + window.GetType().Name);
                }
                dcs.DcsContext.Select(dcs.DcsContext.State.Selection with { ControllerId = MixedSnapshotFixture.Supported.ToString("N") });
                if (dcs.DcsContext.Context.ReadOnly) throw new InvalidOperationException("Connected shared DCS hardware was incorrectly read-only.");
                native.Observations = [];
                if (services.Discover().Count != 0 || !dcs.DcsContext.Context.ReadOnly)
                    throw new InvalidOperationException("An offline controller remained selectable or writable.");
                native.Observations = [observation];
                if (services.Discover().Count != 1 || dcs.DcsContext.Context.ReadOnly)
                    throw new InvalidOperationException("Reconnecting the same physical unit did not restore availability.");
                if (store.List().Count != 0 || before != DcsEditorSnapshotSerializer.Serialize(workspace) || dcs.Session.HasPendingChanges)
                    throw new InvalidOperationException("Shared device availability modified game bindings or created game profiles.");
            }
            finally
            {
                foreach (var window in windows.AsEnumerable().Reverse()) window.Close();
                EditionAccess.SetForTests(previous);
                Directory.Delete(root, true);
            }
        }
        #endregion END Public
    }
}
