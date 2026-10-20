using System.Text.Json;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects reviewed inventory, modules and recovery across rescans and native failures.</summary>
    public sealed class HardwareInventoryTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(),"ipia-inventory-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>Repeated identical units keep unique GUIDs, labels, selections and definitions after reopen.</summary>
        [Fact]
        public async Task Explicit_adoption_survives_reopen_disconnect_and_changed_capabilities()
        {
            var repository = await Repository("data");
            var one = Observed(Guid.NewGuid(),1);
            var two = Observed(Guid.NewGuid(),1);
            var fake = new FakeControllerCapabilityDiscovery { Observations = [one,two] };
            var manager = new HardwareInventoryManager(repository,fake);
            var rows = await manager.ScanAsync();
            Assert.All(rows,x => Assert.Null(x.Saved));
            Assert.Empty((await repository.ReadAsync()).Instances);
            await manager.SaveAsync(rows.Select((x,i) => new HardwareInventoryEdit(x,true,"Panel " + i,"role " + i,false,[])).ToArray());
            var stored = await repository.ReadAsync();
            Assert.Equal(2,stored.Instances.Count);
            Assert.Single(stored.Definitions);
            Assert.Equal(2,stored.Instances.Select(x => x.Label).Distinct().Count());
            Assert.All(stored.Observations,x => Assert.False(x.Device.Connected));
            var reopened = await new HardwareInventoryManager(repository,fake).LoadAsync();
            Assert.All(reopened,x => Assert.Null(x.Observed));
            fake.Observations = [one with { Capabilities = Buttons(2) }];
            rows = await manager.ScanAsync();
            var changed = rows.Single(x => x.PhysicalGuid == one.Device.PhysicalGuid);
            Assert.True(changed.Changed);
            Assert.Null(rows.Single(x => x.PhysicalGuid == two.Device.PhysicalGuid).Observed);
            await manager.SaveAsync(rows.Select(x => new HardwareInventoryEdit(x,x.Saved!.Selected,x.Saved.Label,x.Saved.Role,false,[])).ToArray());
            Assert.Single((await repository.ReadAsync()).Definitions);
            rows = await manager.ScanAsync();
            await manager.SaveAsync(rows.Select(x => new HardwareInventoryEdit(x,x.Saved!.Selected,x.Saved.Label,x.Saved.Role,x.Changed,["compatible-hint"])).ToArray());
            Assert.Equal(2,(await repository.ReadAsync()).Definitions.Count);
            Assert.Equal(2,(await manager.ScanAsync()).Count);
            fake.Observations = [];
            Assert.All(await manager.ScanAsync(),x => { Assert.True(x.Saved!.Selected); Assert.Null(x.Observed); });
            fake.Observations = [one with { Capabilities = Buttons(2) },two];
            Assert.All(await manager.ScanAsync(),x => Assert.False(x.Changed));
        }

        /// <summary>Global failures, no devices, per-device failures and cancellation never erase selection.</summary>
        [Fact]
        public async Task Failure_and_cancellation_do_not_write_or_remove_devices()
        {
            var repository = await Repository("data");
            var observation = Observed(Guid.NewGuid(),1);
            var fake = new FakeControllerCapabilityDiscovery { Observations = [observation] };
            var manager = new HardwareInventoryManager(repository,fake);
            var row = Assert.Single(await manager.ScanAsync());
            await manager.SaveAsync([new(row,true,"Saved","",false,[])]);
            var before = HardwareInventoryContract.Export(await repository.ReadAsync());
            fake.Failure = new IOException("native discovery failed");
            await Assert.ThrowsAsync<IOException>(() => manager.ScanAsync());
            Assert.Equal(before,HardwareInventoryContract.Export(await repository.ReadAsync()));
            fake.Failure = null;
            fake.Observations = [observation with { Capabilities = null, Error = "unplugged during enumeration" }];
            row = Assert.Single(await manager.ScanAsync());
            Assert.NotNull(row.Observed!.Error);
            await manager.SaveAsync([new(row,true,"Saved","",false,[])]);
            Assert.Single((await repository.ReadAsync()).Instances);
            var cancelFake = new FakeControllerCapabilityDiscovery { WaitForCancellation = true };
            using var cancel = new CancellationTokenSource();
            var pending = new HardwareInventoryManager(repository,cancelFake).ScanAsync(cancel.Token);
            await cancelFake.Started.Task;
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            fake.Observations = [];
            Assert.Single(await manager.ScanAsync());
            Assert.Empty(await new HardwareInventoryManager(await Repository("empty"),fake).ScanAsync());
        }

        /// <summary>Invalid adoption and stale/duplicate edits fail before persistence.</summary>
        [Fact]
        public async Task Unsafe_review_is_rejected_and_unknown_units_require_supported_evidence()
        {
            var repository = await Repository("data");
            var observed = Observed(Guid.NewGuid(),0);
            var fake = new FakeControllerCapabilityDiscovery { Observations = [observed] };
            var manager = new HardwareInventoryManager(repository,fake);
            var row = Assert.Single(await manager.ScanAsync());
            await manager.SaveAsync([new(row,false,"Not adopted","",false,[])]);
            Assert.Empty((await repository.ReadAsync()).Instances);
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SaveAsync([new(row,true,"Invalid","",false,[])]));
            fake.Observations = [observed with { Capabilities = Buttons(1) }];
            row = Assert.Single(await manager.ScanAsync());
            await Assert.ThrowsAsync<InvalidDataException>(() => manager.SaveAsync([new(row,true,"","",false,[])]));
            await Assert.ThrowsAsync<InvalidDataException>(() => manager.SaveAsync([new(row,true,"A","",false,[]),new(row,true,"B","",false,[])]));
            await manager.SaveAsync([new(row,true,"Valid","",false,[])]);
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SaveAsync([new(row,true,"Stale","",false,[])]));
            fake.Observations = [observed,observed];
            await Assert.ThrowsAsync<InvalidDataException>(() => manager.ScanAsync());
            row = Assert.Single(await manager.LoadAsync());
            await manager.SaveAsync([new(row,false,"Retained offline","role",false,[])]);
            Assert.False(Assert.Single((await repository.ReadAsync()).Instances).Selected);
        }

        /// <summary>Migration 8 and full user-data recovery preserve modules and portable inventory.</summary>
        [Fact]
        public async Task Migration_snapshot_backup_restore_and_profile_isolation_preserve_modules()
        {
            var repository = await Repository("data");
            Execute("data","INSERT INTO modules(id,dcs_id,display_name) VALUES('module','OLD','Existing module'); DROP TABLE hardware_inventory; DROP TABLE hardware_definitions; DROP TABLE hardware_observations; DELETE FROM schema_migrations WHERE version=8");
            await new SqliteMigrationRunner(Connection("data")).MigrateAsync();
            var fake = new FakeControllerCapabilityDiscovery { Observations = [Observed(Guid.NewGuid(),3)] };
            var manager = new HardwareInventoryManager(repository,fake);
            await manager.SaveAsync([new(Assert.Single(await manager.ScanAsync()),true,"Unknown panel","Left",false,[])]);
            Assert.Empty((await (await Repository("other")).ReadAsync()).Instances);
            var snapshot = await repository.ReadAsync();
            var portable = HardwareInventoryContract.Import(HardwareInventoryContract.Export(snapshot));
            Assert.Equal(HardwareInventoryContract.Export(snapshot),HardwareInventoryContract.Export(portable));
            var data = Path.Combine(_folder,"data");
            var recovery = new UserDataRecoveryService(data,"1.0");
            var backup = Path.Combine(_folder,"inventory.zip");
            recovery.Backup(backup);
            Assert.Equal(SqliteMigrationRunner.CurrentVersion,recovery.Inspect(backup).DatabaseSchemaVersion);
            await repository.SaveAsync(new(1,[],[],[]));
            recovery.Restore(backup);
            Assert.Single((await repository.ReadAsync()).Instances);
            using var db = new SqliteConnection(Connection("data"));
            db.Open();
            using var command = db.CreateCommand();command.CommandText = "SELECT display_name FROM modules WHERE id='module'";
            Assert.Equal("Existing module",command.ExecuteScalar());
        }

        /// <summary>Corrupt/newer snapshots and invalid native interpretations cannot overwrite saved inventory.</summary>
        [Fact]
        public async Task Snapshot_contract_rejects_corruption_before_writes()
        {
            var repository = await Repository("data");
            var caps = Buttons(1);
            var id = ControllerCapabilityMapper.Fingerprint(caps);
            var unit = new HardwareInventoryInstance(Guid.NewGuid(),"Native",id,"Label","",true,[]);
            var snapshot = new HardwareInventorySnapshot(1,[new(id,caps)],[unit],[]);
            await repository.SaveAsync(snapshot);
            Assert.Throws<InvalidDataException>(() => HardwareInventoryContract.Validate(snapshot with { SchemaVersion = 2 }));
            Assert.Throws<InvalidDataException>(() => HardwareInventoryContract.Import("null"));
            Assert.Throws<JsonException>(() => HardwareInventoryContract.Import("{"));
            await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(snapshot with { Definitions = [] }));
            await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(snapshot with { Instances = [unit,unit] }));
            Assert.Throws<InvalidDataException>(() => HardwareInventoryContract.Validate(snapshot with { Definitions = [new("wrong",caps)] }));
            var invalid = caps with { Inputs = [caps.Inputs[0] with { Offset = 1 }] };
            Assert.Throws<InvalidDataException>(() => HardwareInventoryContract.Validate(snapshot with { Definitions = [new(id,invalid)] }));
            Assert.Single((await repository.ReadAsync()).Instances);
            using var cancel = new CancellationTokenSource();cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(snapshot,cancel.Token));
            Assert.Single((await repository.ReadAsync()).Instances);
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_folder)) Directory.Delete(_folder,true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task<SqliteHardwareInventoryRepository> Repository(string name)
        {
            Directory.CreateDirectory(Path.Combine(_folder,name));
            await new SqliteMigrationRunner(Connection(name)).MigrateAsync();
            return new(Connection(name));
        }
        private string Connection(string name) => new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(_folder,name,"ipia.db"), Pooling = false, ForeignKeys = true }.ToString();
        private void Execute(string name,string sql)
        {
            using var db = new SqliteConnection(Connection(name));db.Open();
            using var command = db.CreateCommand();command.CommandText = sql;command.ExecuteNonQuery();
        }
        private static ControllerObservation Observed(Guid guid,int buttons) => new(new(guid,"Identical panel",true),Buttons(buttons));
        private static ControllerCapabilities Buttons(int count) => ControllerCapabilityMapper.Map(
            Enumerable.Range(0,count).Select(x => new ControllerObject(new Guid("a36d02f0-c9f3-11cf-bfc7-444553540000"),12u | ((uint)x << 8),"Button " + (x + 1))));
        #endregion END private helpers
    }
}
