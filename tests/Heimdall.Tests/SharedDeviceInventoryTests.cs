using Heimdall.Application;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects one shared device inventory, live-only availability and portable drawing preferences.</summary>
    public sealed class SharedDeviceInventoryTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall-shared-devices-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>Exact adopted physical identities are shared; unplugging never deletes saved units or preferences.</summary>
        [Fact]
        public async Task Connected_choices_use_live_adopted_guids_and_preserve_offline_units()
        {
            var repository = await Repository();
            var one = Observation(Guid.NewGuid());
            var duplicateName = Observation(Guid.NewGuid());
            var native = new InventoryTestDiscovery { Observations = [one, duplicateName] };
            var inventory = new HardwareInventoryManager(repository, native);
            var rows = await inventory.ScanAsync();
            await inventory.SaveAsync(rows.Select(row => new HardwareInventoryEdit(row,
                row.PhysicalGuid == one.Device.PhysicalGuid, "My controller", "Pilot", false, [])).ToArray());
            var discovery = new InventoryDeviceDiscovery(inventory, native);
            Assert.Equal(one.Device.PhysicalGuid, Assert.Single(discovery.Discover()).PhysicalGuid);
            Assert.Equal(one.Device.Name, Assert.Single(discovery.Discover()).Name);
            Assert.Contains("My controller", Assert.Single(discovery.Discover()).Title, StringComparison.Ordinal);
            Assert.Equal(one.Device.FileName, Assert.Single(discovery.Discover()).FileName);
            Assert.Equal(one.Device.PhysicalGuid, Assert.Single(await discovery.DiscoverAsync()).Device.PhysicalGuid);
            var before = HardwareInventoryContract.Export(await repository.ReadAsync());
            native.Observations = [duplicateName];
            Assert.Empty(discovery.Discover());
            Assert.Empty(await discovery.DiscoverAsync());
            Assert.Equal(before, HardwareInventoryContract.Export(await repository.ReadAsync()));
            Assert.True(Assert.Single(inventory.Current.Instances).Selected);
            native.Observations = [one];
            Assert.Equal(one.Device.PhysicalGuid, Assert.Single(discovery.Discover()).PhysicalGuid);
            Assert.Equal(one.Device.PhysicalGuid, Assert.Single(await discovery.DiscoverAsync()).Device.PhysicalGuid);
        }

        /// <summary>Shared artwork survives SQLite reopen and label-only edits, while an explicit generic choice clears it.</summary>
        [Fact]
        public async Task Preferred_drawing_round_trips_without_a_schema_change_or_implicit_replacement()
        {
            var repository = await Repository();
            var native = new InventoryTestDiscovery { Observations = [Observation(Guid.NewGuid())] };
            var inventory = new HardwareInventoryManager(repository, native);
            var row = Assert.Single(await inventory.ScanAsync());
            await inventory.SaveAsync([new(row, true, "Left", "Pilot", false, ["drawing-a"], true)]);
            Assert.Equal("drawing-a", Assert.Single((await repository.ReadAsync()).Instances).PreferredHardwareId);
            var saved = await inventory.ReadSnapshotAsync();
            Assert.Throws<InvalidDataException>(() => HardwareInventoryContract.Validate(saved with
            {
                Instances = [saved.Instances[0] with { PreferredHardwareId = "unreviewed-drawing" }]
            }));
            var reopened = new HardwareInventoryManager(repository, native);
            Assert.Empty(reopened.Current.Instances);
            await reopened.ReadSnapshotAsync();
            Assert.Equal("drawing-a", Assert.Single(reopened.Current.Instances).PreferredHardwareId);
            row = Assert.Single(await reopened.LoadAsync());
            await reopened.SaveAsync([new(row, true, "Renamed offline", "Pilot", false, [])]);
            Assert.Equal("drawing-a", Assert.Single(reopened.Current.Instances).PreferredHardwareId);
            row = Assert.Single(await reopened.LoadAsync());
            await reopened.SaveAsync([new(row, true, "Renamed offline", "Pilot", false, [], true)]);
            Assert.Null(Assert.Single(reopened.Current.Instances).PreferredHardwareId);
            var oldJson = HardwareInventoryContract.Export(reopened.Current).Replace(
                ",\"PreferredHardwareId\":null", "", StringComparison.Ordinal);
            Assert.Null(Assert.Single(HardwareInventoryContract.Import(oldJson).Instances).PreferredHardwareId);
        }

        /// <summary>Changed or failed capability discovery never supplies unreviewed controls or rewrites inventory.</summary>
        [Fact]
        public async Task Failed_or_changed_discovery_keeps_saved_definitions_and_does_not_adopt_controls()
        {
            var repository = await Repository();
            var observed = Observation(Guid.NewGuid());
            var native = new InventoryTestDiscovery { Observations = [observed] };
            var inventory = new HardwareInventoryManager(repository, native);
            var row = Assert.Single(await inventory.ScanAsync());
            await inventory.SaveAsync([new(row, true, "Controller", "", false, [])]);
            var before = HardwareInventoryContract.Export(await inventory.ReadSnapshotAsync());
            var discovery = new InventoryDeviceDiscovery(inventory, native);
            native.Observations = [observed with { Capabilities = ControllerCapabilityMapper.Map([
                new(Guid.NewGuid(), 12, "Changed button"), new(Guid.NewGuid(), 268, "Extra button")]) }];
            Assert.Empty(await discovery.DiscoverAsync());
            Assert.Equal(before, HardwareInventoryContract.Export(inventory.Current));
            native.Failure = new IOException("Native scan failed.");
            await Assert.ThrowsAsync<IOException>(() => discovery.DiscoverAsync());
            Assert.Equal(before, HardwareInventoryContract.Export(await repository.ReadAsync()));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => discovery.DiscoverAsync(cancellation.Token));
        }

        /// <summary>Releases the isolated inventory database.</summary>
        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task<SqliteHardwareInventoryRepository> Repository()
        {
            Directory.CreateDirectory(_root);
            var connection = "Data Source=" + Path.Combine(_root, "inventory.db") + ";Pooling=False";
            await new SqliteMigrationRunner(connection).MigrateAsync();
            return new(connection);
        }
        private static ControllerObservation Observation(Guid id) => new(new(id, "Identical panel", true),
            ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button")]));
        #endregion END private helpers
    }
}
