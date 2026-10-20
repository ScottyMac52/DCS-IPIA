using Heimdall.Application;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects first-run, offline, transactional confirmation and recoverable rerun behavior.</summary>
    public sealed class SetupTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ipia-setup-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>Discovery failures and cancelled scans leave completion and adoption untouched.</summary>
        [Fact]
        public async Task Discovery_and_cancel_are_read_only()
        {
            var connection = await Create();
            var repository = new SqliteHardwareInventoryRepository(connection);
            var fake = new FakeControllerCapabilityDiscovery { Observations = [Observed()] };
            var inventory = new HardwareInventoryManager(repository, fake);
            var setup = new SetupManager(inventory, new SqliteSetupRepository(connection));
            Assert.False((await setup.ReadAsync()).Completed);
            Assert.Single(await inventory.ScanAsync());
            Assert.Empty((await repository.ReadAsync()).Instances);
            fake.Failure = new IOException("Discovery unavailable");
            await Assert.ThrowsAsync<IOException>(() => inventory.ScanAsync());
            fake.Failure = null;
            fake.WaitForCancellation = true;
            using var cancel = new CancellationTokenSource();
            var pending = inventory.ScanAsync(cancel.Token);
            await fake.Started.Task;
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.False((await setup.ReadAsync()).Completed);
            Assert.Empty((await repository.ReadAsync()).Instances);
        }

        /// <summary>Confirmed generic units, destination and completion survive restart and verified recovery.</summary>
        [Fact]
        public async Task Confirm_reopen_backup_restore_and_profile_isolation()
        {
            var connection = await Create();
            var repository = new SqliteHardwareInventoryRepository(connection);
            var fake = new FakeControllerCapabilityDiscovery { Observations = [Observed(), Observed()] };
            var inventory = new HardwareInventoryManager(repository, fake);
            var setup = new SetupManager(inventory, new SqliteSetupRepository(connection));
            var rows = await inventory.ScanAsync();
            var destination = Path.Combine(_root, "Saved Games", "DCS");
            await setup.ConfirmAsync(rows.Select((row, index) => Edit(row, "Panel " + index)).ToArray(), destination, false);
            var stored = await repository.ReadAsync();
            Assert.Equal(2, stored.Instances.Count);
            Assert.Single(stored.Definitions);
            Assert.All(stored.Instances, x => Assert.Empty(x.TemplateCandidates));
            Assert.All(stored.Observations, x => Assert.False(x.Device.Connected));
            Assert.Equal(new SetupState(true, destination), await setup.ReadAsync());
            Assert.False(Directory.Exists(destination));
            var profile = UserDataProfile.Select(_root, "fresh");
            Assert.True(new UserDataProfileService(profile, "1").SetupCompleted);
            var settings = Settings(connection, profile.DataDirectory);
            Assert.Equal(destination, settings.Current.SavedGamesDcsFolder);
            Assert.True(settings.TryUpdateTheme(AppearanceTheme.Dark));
            var recovery = new UserDataRecoveryService(profile.DataDirectory, "1");
            var archive = Path.Combine(_root, "confirmed.zip");
            recovery.Backup(archive);
            new UserDataProfileService(profile, "1").SetSetupCompleted(false);
            Assert.False((await setup.ReadAsync()).Completed);
            recovery.Restore(archive);
            Assert.True((await setup.ReadAsync()).Completed);
            Assert.Equal(destination, Settings(connection, profile.DataDirectory).Current.SavedGamesDcsFolder);
            Assert.Equal(2, (await inventory.LoadAsync()).Count);
            var other = await Create("other");
            Assert.False((await new SqliteSetupRepository(other).ReadAsync()).Completed);
            Assert.Empty((await new SqliteHardwareInventoryRepository(other).ReadAsync()).Instances);
        }

        /// <summary>Failure after inventory writes rolls everything back, including completion and destination.</summary>
        [Fact]
        public async Task Failed_transaction_and_cancelled_confirmation_do_not_partially_adopt()
        {
            var connection = await Create();
            var repository = new SqliteHardwareInventoryRepository(connection);
            var inventory = new HardwareInventoryManager(repository, new FakeControllerCapabilityDiscovery { Observations = [Observed()] });
            var setup = new SetupManager(inventory, new SqliteSetupRepository(connection));
            var edits = new[] { Edit(Assert.Single(await inventory.ScanAsync()), "Reviewed") };
            Execute(connection, "CREATE TRIGGER fail_setup BEFORE UPDATE ON profile_setup BEGIN SELECT RAISE(ABORT,'injected commit failure'); END");
            await Assert.ThrowsAsync<SqliteException>(() => setup.ConfirmAsync(edits, _root, false));
            Assert.Empty((await repository.ReadAsync()).Instances);
            Assert.Empty((await repository.ReadAsync()).Definitions);
            Assert.Equal(new SetupState(false, null), await setup.ReadAsync());
            Execute(connection, "DROP TRIGGER fail_setup");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => setup.ConfirmAsync(edits, _root, false, cancel.Token));
            Assert.Empty((await repository.ReadAsync()).Instances);
            Assert.False((await setup.ReadAsync()).Completed);
            await setup.ConfirmAsync(edits, _root, false);
            Assert.True((await setup.ReadAsync()).Completed);
        }

        /// <summary>Offline completion is explicit; invalid paths and unsupported units remain deferred.</summary>
        [Fact]
        public async Task Offline_setup_requires_consent_and_valid_review()
        {
            var connection = await Create();
            var fake = new FakeControllerCapabilityDiscovery();
            var inventory = new HardwareInventoryManager(new SqliteHardwareInventoryRepository(connection), fake);
            var setup = new SetupManager(inventory, new SqliteSetupRepository(connection));
            await Assert.ThrowsAsync<InvalidOperationException>(() => setup.ConfirmAsync([], "", false));
            await Assert.ThrowsAsync<ArgumentException>(() => setup.ConfirmAsync([], "relative/DCS", true));
            fake.Observations = [Observed() with { Capabilities = null, Error = "No supported controls" }];
            var unsupported = Assert.Single(await inventory.ScanAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => setup.ConfirmAsync([Edit(unsupported, "Panel")], "", true));
            Assert.False((await setup.ReadAsync()).Completed);
            await setup.ConfirmAsync([], "", true);
            Assert.Equal(new SetupState(true, ""), await setup.ReadAsync());
        }

        /// <summary>Reruns retain disconnected instances and modules; capability changes need explicit adoption.</summary>
        [Fact]
        public async Task Rerun_retains_data_and_pinned_capabilities()
        {
            var connection = await Create();
            Execute(connection, "INSERT INTO modules(id,dcs_id,display_name) VALUES('keep','EXISTING','Retained')");
            var repository = new SqliteHardwareInventoryRepository(connection);
            var observed = Observed();
            var fake = new FakeControllerCapabilityDiscovery { Observations = [observed] };
            var inventory = new HardwareInventoryManager(repository, fake);
            var setup = new SetupManager(inventory, new SqliteSetupRepository(connection));
            await setup.ConfirmAsync([Edit(Assert.Single(await inventory.ScanAsync()), "My panel")], _root, false);
            var pinned = Assert.Single((await repository.ReadAsync()).Instances).DefinitionId;
            var lifecycle = new UserDataProfileService(UserDataProfile.Select(_root, "fresh"), "1");
            lifecycle.SetSetupCompleted(false);
            fake.Observations = [];
            var offline = Assert.Single(await inventory.ScanAsync());
            Assert.Equal("My panel", offline.Saved!.Label);
            Assert.False(lifecycle.SetupCompleted);
            fake.Observations = [observed with { Capabilities = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button"), new(Guid.NewGuid(), 268, "Button")]) }];
            var changed = Assert.Single(await inventory.ScanAsync());
            Assert.True(changed.Changed);
            await setup.ConfirmAsync([Edit(changed, "My panel")], _root, false);
            Assert.Equal(pinned, Assert.Single((await repository.ReadAsync()).Instances).DefinitionId);
            changed = Assert.Single(await inventory.ScanAsync());
            await setup.ConfirmAsync([Edit(changed, "My panel") with { AdoptChangedCapabilities = true }], _root, false);
            Assert.NotEqual(pinned, Assert.Single((await repository.ReadAsync()).Instances).DefinitionId);
            using var db = new SqliteConnection(connection);
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT display_name FROM modules WHERE id='keep'";
            Assert.Equal("Retained", command.ExecuteScalar());
        }

        /// <summary>Stale review cannot replace independently saved inventory.</summary>
        [Fact]
        public async Task Stale_inventory_is_rejected_before_commit()
        {
            var connection = await Create();
            var repository = new SqliteHardwareInventoryRepository(connection);
            var inventory = new HardwareInventoryManager(repository, new FakeControllerCapabilityDiscovery { Observations = [Observed()] });
            var expected = await repository.ReadAsync();
            var reviewed = HardwareInventoryManager.Prepare(expected, [Edit(Assert.Single(await inventory.ScanAsync()), "Panel")]);
            await repository.SaveAsync(reviewed);
            var setup = new SqliteSetupRepository(connection);
            await Assert.ThrowsAsync<InvalidOperationException>(() => setup.CommitAsync(expected, reviewed, _root));
            Assert.False((await setup.ReadAsync()).Completed);
            Assert.Single((await repository.ReadAsync()).Instances);
        }

        /// <summary>Upgrades preserve existing modules and destination files; legacy markers can be reset.</summary>
        [Fact]
        public async Task Upgrade_and_legacy_completion_are_compatible()
        {
            var connection = await Create();
            Execute(connection, "INSERT INTO modules(id,dcs_id,display_name) VALUES('keep','OLD','Old module'); DROP TABLE profile_setup; DELETE FROM schema_migrations WHERE version>=9");
            await new SqliteMigrationRunner(connection).MigrateAsync();
            Assert.Equal(new SetupState(true, null), await new SqliteSetupRepository(connection).ReadAsync());
            var profile = UserDataProfile.Select(_root, "fresh");
            File.WriteAllText(Path.Combine(profile.DataDirectory, "onboarding-state.json"), "{\"version\":1,\"completed\":true}");
            var lifecycle = new UserDataProfileService(profile, "1");
            Assert.True(lifecycle.SetupCompleted);
            Assert.True(lifecycle.SetupOffered);
            lifecycle.SetSetupCompleted(false);
            Assert.False(lifecycle.SetupCompleted);
            Assert.False(File.Exists(Path.Combine(profile.DataDirectory, "onboarding-state.json")));
            await new SqliteMigrationRunner(connection).MigrateAsync();
            Assert.False(lifecycle.SetupCompleted);
        }

        /// <summary>An offered but unfinished setup persists independently, follows full recovery, and resets with its profile.</summary>
        [Fact]
        public async Task Setup_offer_survives_restart_and_recovery_without_adopting_hardware()
        {
            var connection = await Create();
            var profile = UserDataProfile.Select(_root, "fresh");
            var lifecycle = new UserDataProfileService(profile, "1");
            Assert.False(lifecycle.SetupOffered);
            lifecycle.MarkSetupOffered();
            lifecycle.MarkSetupOffered();
            Assert.True(new UserDataProfileService(UserDataProfile.Select(_root, "fresh"), "1").SetupOffered);
            Assert.False(lifecycle.SetupCompleted);
            Assert.Equal(new SetupState(false, null), await new SqliteSetupRepository(connection).ReadAsync());
            Assert.Empty((await new SqliteHardwareInventoryRepository(connection).ReadAsync()).Instances);
            await Create("other");
            Assert.False(new UserDataProfileService(UserDataProfile.Select(_root, "other"), "1").SetupOffered);
            var recovery = new UserDataRecoveryService(profile.DataDirectory, "1");
            var archive = Path.Combine(_root, "offered.zip");
            recovery.Backup(archive);
            Execute(connection, "UPDATE profile_setup SET offered=0 WHERE id=1");
            Assert.False(lifecycle.SetupOffered);
            recovery.Restore(archive);
            Assert.True(lifecycle.SetupOffered);
            Assert.False(lifecycle.SetupCompleted);
            lifecycle.Reset(true, Path.Combine(_root, "before-offer-reset.zip"));
            Assert.False(lifecycle.SetupOffered);
            Assert.False(lifecycle.SetupCompleted);
        }

        /// <summary>Schema 9 upgrades preserve completion and are safe to repeat without offering setup to established profiles.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Schema_9_upgrade_separates_offered_and_completed(bool completed)
        {
            var connection = await Create();
            Execute(connection, "ALTER TABLE profile_setup DROP COLUMN offered; DELETE FROM schema_migrations WHERE version=10; " +
                "UPDATE profile_setup SET completed=" + (completed ? "1" : "0"));
            await new SqliteMigrationRunner(connection).MigrateAsync();
            await new SqliteMigrationRunner(connection).MigrateAsync();
            var lifecycle = new UserDataProfileService(UserDataProfile.Select(_root, "fresh"), "1");
            Assert.Equal(completed, lifecycle.SetupCompleted);
            Assert.Equal(completed, lifecycle.SetupOffered);
            lifecycle.MarkSetupOffered();
            Assert.True(lifecycle.SetupOffered);
            Assert.Equal(completed, lifecycle.SetupCompleted);
        }

        /// <summary>A failed offer write is visible to callers and leaves a fresh profile eligible on its next start.</summary>
        [Fact]
        public async Task Failed_offer_write_does_not_suppress_setup()
        {
            var connection = await Create();
            var lifecycle = new UserDataProfileService(UserDataProfile.Select(_root, "fresh"), "1");
            Execute(connection, "CREATE TRIGGER fail_offer BEFORE UPDATE ON profile_setup BEGIN SELECT RAISE(ABORT,'injected offer failure'); END");
            Assert.Throws<SqliteException>(() => lifecycle.MarkSetupOffered());
            Assert.False(lifecycle.SetupOffered);
            Execute(connection, "DROP TRIGGER fail_offer; DELETE FROM profile_setup");
            Assert.Throws<InvalidDataException>(() => lifecycle.MarkSetupOffered());
        }

        /// <summary>Releases temporary profile data and SQLite pools.</summary>
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task<string> Create(string name = "fresh")
        {
            var profile = UserDataProfile.Select(_root, name);
            Directory.CreateDirectory(profile.DataDirectory);
            var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(profile.DataDirectory, "ipia.db"), Pooling = false, ForeignKeys = true }.ToString();
            await new SqliteMigrationRunner(connection).MigrateAsync();
            return connection;
        }
        private static ControllerObservation Observed() => new(new(Guid.NewGuid(), "Unbundled panel", true),
            ControllerCapabilityMapper.Map([new(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12, "Button")]));
        private static HardwareInventoryEdit Edit(HardwareInventoryCandidate row, string label) => new(row, true, label, "", false, []);
        private static void Execute(string connection, string sql)
        {
            using var db = new SqliteConnection(connection);
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        private static SqliteAppSettingsService Settings(string connection, string folder) => new(connection,
            Path.Combine(folder, "defaults.json"), Path.Combine(folder, "settings.json"), Path.Combine(folder, "theme.txt"));
        #endregion END private helpers
    }
}
