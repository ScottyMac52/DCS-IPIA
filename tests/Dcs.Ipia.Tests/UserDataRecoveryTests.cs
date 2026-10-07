using System.IO.Compression;
using System.Text.Json;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises real SQLite backups, hostile archives and recoverable directory replacement.</summary>
    public sealed class UserDataRecoveryTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "ipia-recovery-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>A committed live database, settings and future assets survive a portable round trip.</summary>
        [Fact]
        public async Task Full_backup_restores_committed_data_and_external_assets()
        {
            var root = await Create("source", "saved");
            File.WriteAllText(Path.Combine(root, "appsettings.json"), "{\"SavedGamesDcsFolder\":\"DCS custom\"}");
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            File.WriteAllText(Path.Combine(root, "assets", "custom.drawio"), "original artwork");
            using var live = new SqliteConnection(Connection(root));
            live.Open();
            using var command = live.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; UPDATE modules SET display_name='committed'";
            command.ExecuteNonQuery();
            var archive = Path.Combine(_folder, "backup.zip");
            var service = new UserDataRecoveryService(root, "1.2.3.4");
            service.Backup(archive);
            Assert.Equal(SqliteMigrationRunner.CurrentVersion, service.Inspect(archive).DatabaseSchemaVersion);
            var target = await Create("target", "old");
            var previous = new UserDataRecoveryService(target, "1.2.3.4").Restore(archive);
            Assert.Equal("committed", Name(target));
            Assert.Equal("old", Name(previous));
            Assert.Equal("original artwork", File.ReadAllText(Path.Combine(target, "assets", "custom.drawio")));
            Assert.Equal(File.ReadAllText(Path.Combine(root, "appsettings.json")), File.ReadAllText(Path.Combine(target, "appsettings.json")));
            Assert.False(File.Exists(Path.Combine(target, "ipia.db-wal")));
        }

        /// <summary>Modified payload hashes and incompatible schemas are rejected before changing live data.</summary>
        [Theory]
        [InlineData("hash")]
        [InlineData("newer")]
        [InlineData("truncated")]
        public async Task Invalid_backup_leaves_live_database_unchanged(string failure)
        {
            var source = await Create("source", "saved");
            var target = await Create("target", "current");
            var archive = Path.Combine(_folder, "backup.zip");
            var service = new UserDataRecoveryService(source, "1.2.3.4");
            if (failure == "newer") Execute(source, "INSERT INTO schema_migrations VALUES(99,'future')");
            if (failure == "newer") Assert.Throws<InvalidDataException>(() => service.Backup(archive));
            else
            {
                service.Backup(archive);
                if (failure == "hash")
                {
                    using var zip = ZipFile.Open(archive, ZipArchiveMode.Update);
                    zip.GetEntry("ipia.db")!.Delete();
                    using var writer = new StreamWriter(zip.CreateEntry("ipia.db").Open());
                    writer.Write("changed");
                }
                else File.WriteAllText(archive, "not a zip");
                Assert.ThrowsAny<Exception>(() => new UserDataRecoveryService(target, "1.2.3.4").Restore(archive));
            }
            Assert.Equal("current", Name(target));
        }

        /// <summary>Unsafe and unmanifested entries cannot escape or enter the restored profile.</summary>
        [Theory]
        [InlineData("../escape")]
        [InlineData("/absolute")]
        [InlineData("C:/drive")]
        [InlineData("assets\\escape")]
        [InlineData("extra.txt")]
        [InlineData("ipia.db")]
        public async Task Rejects_unsafe_extra_and_duplicate_entries(string entry)
        {
            var root = await Create("source", "saved");
            var archive = Path.Combine(_folder, "backup.zip");
            var service = new UserDataRecoveryService(root, "1.2.3.4");
            service.Backup(archive);
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
            using (var writer = new StreamWriter(zip.CreateEntry(entry).Open())) writer.Write("hostile");
            Assert.Throws<InvalidDataException>(() => service.Inspect(archive));
            Assert.Equal("saved", Name(root));
        }

        /// <summary>Failure at every directory-swap boundary rolls back to the previous data.</summary>
        [Theory]
        [InlineData("prepared")]
        [InlineData("old-moved")]
        [InlineData("installed")]
        public async Task Failed_restore_rolls_back(string checkpoint)
        {
            var source = await Create("source", "saved");
            var target = await Create("target", "current");
            var archive = Path.Combine(_folder, "backup.zip");
            new UserDataRecoveryService(source, "1.2.3.4").Backup(archive);
            var service = new UserDataRecoveryService(target, "1.2.3.4", point =>
            {
                if (point == checkpoint) throw new IOException("Injected interruption");
            });
            Assert.Throws<IOException>(() => service.Restore(archive));
            Assert.Equal("current", Name(target));
            service.RecoverInterruptedRestore();
            Assert.Equal("current", Name(target));
        }

        /// <summary>Startup recovers a killed restore after the old directory was renamed.</summary>
        [Fact]
        public async Task Startup_recovers_interrupted_swap()
        {
            var root = await Create("live", "current");
            var id = Guid.NewGuid().ToString("N");
            var stage = root + ".restore-" + id;
            var previous = root + ".before-restore-" + id;
            Directory.CreateDirectory(stage);
            Directory.Move(root, previous);
            File.WriteAllText(root + ".restore.json", JsonSerializer.Serialize(new { Stage = stage, Previous = previous }));
            new UserDataRecoveryService(root, "1.2.3.4").RecoverInterruptedRestore();
            Assert.Equal("current", Name(root));
            Assert.False(Directory.Exists(stage));
            Assert.False(File.Exists(root + ".restore.json"));
        }

        /// <summary>Recovery refuses a forged journal that references another directory.</summary>
        [Fact]
        public async Task Rejects_unsafe_recovery_journal()
        {
            var root = await Create("live", "current");
            File.WriteAllText(root + ".restore.json", JsonSerializer.Serialize(new { Stage = _folder, Previous = _folder }));
            Assert.Throws<InvalidDataException>(() => new UserDataRecoveryService(root, "1.2.3.4").RecoverInterruptedRestore());
            Assert.Equal("current", Name(root));
        }

        /// <summary>A completed replacement is verified, or the previous directory is recovered if damaged.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Startup_verifies_completed_replacement(bool damaged)
        {
            var root = await Create("live", "current");
            var id = Guid.NewGuid().ToString("N");
            var stage = root + ".restore-" + id;
            var previous = root + ".before-restore-" + id;
            Directory.Move(root, previous);
            Directory.CreateDirectory(root);
            if (damaged) File.WriteAllText(Path.Combine(root, "ipia.db"), "damaged replacement");
            else
            {
                await new SqliteMigrationRunner(Connection(root)).MigrateAsync();
                Execute(root, "INSERT INTO modules(id,dcs_id,display_name) VALUES('module','TEST','replacement')");
            }
            File.WriteAllText(root + ".restore.json", JsonSerializer.Serialize(new { Stage = stage, Previous = previous }));
            new UserDataRecoveryService(root, "1.2.3.4").RecoverInterruptedRestore();
            Assert.Equal(damaged ? "current" : "replacement", Name(root));
            Assert.False(File.Exists(root + ".restore.json"));
        }

        /// <summary>A corrupt current database can be replaced while retaining its exact original bytes.</summary>
        [Fact]
        public async Task Restore_preserves_corrupt_original_and_blocks_deployment_records()
        {
            var source = await Create("source", "saved");
            Execute(source, "INSERT INTO fly_now_deployment VALUES(1,'{\"Active\":{\"Destination\":\"other computer\"}}')");
            var archive = Path.Combine(_folder, "backup.zip");
            new UserDataRecoveryService(source, "1.2.3.4").Backup(archive);
            var target = Path.Combine(_folder, "corrupt");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "ipia.db"), "original corrupt bytes");
            var service = new UserDataRecoveryService(target, "1.2.3.4");
            var previous = service.Restore(archive);
            Assert.Equal("original corrupt bytes", File.ReadAllText(Path.Combine(previous, "ipia.db")));
            Assert.Contains("other computer", service.DeploymentReview);
            service.ForgetReviewedDeployment();
            Assert.Null(service.DeploymentReview);
            Execute(target, "SELECT count(*) FROM fly_now_deployment");
        }

        /// <summary>Backups cannot be stored inside data that a restore replaces or overwrite without consent.</summary>
        [Fact]
        public async Task Protects_backup_locations_and_existing_archives()
        {
            var root = await Create("source", "saved");
            var service = new UserDataRecoveryService(root, "1.2.3.4");
            Assert.Throws<ArgumentException>(() => service.Backup(Path.Combine(root, "backup.zip")));
            var archive = Path.Combine(_folder, "backup.zip");
            service.Backup(archive);
            Assert.Throws<IOException>(() => service.Backup(archive));
            Assert.Null(service.DeploymentReview);
            service.ForgetReviewedDeployment();
        }

        /// <summary>Integrity alone cannot accept a database with missing tables or broken relational references.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Rejects_incomplete_schema_and_broken_references(bool missingTable)
        {
            var root = await Create("source", "saved");
            if (missingTable) Execute(root, "DROP TABLE app_preferences");
            else Execute(root, "PRAGMA foreign_keys=OFF; INSERT INTO device_profiles VALUES('profile','missing-module','device',NULL,'role','source')");
            var archive = Path.Combine(_folder, "invalid.zip");
            Assert.Throws<InvalidDataException>(() => new UserDataRecoveryService(root, "1.2.3.4").Backup(archive));
            Assert.False(File.Exists(archive));
        }

        /// <summary>Supported old schemas migrate in staging without changing the creating profile.</summary>
        [Fact]
        public async Task Restores_and_migrates_older_schema()
        {
            var source = Path.Combine(_folder, "older");
            Directory.CreateDirectory(source);
            Execute(source, "CREATE TABLE schema_migrations(version INTEGER PRIMARY KEY,applied_utc TEXT); INSERT INTO schema_migrations VALUES(1,'old'); CREATE TABLE modules(id TEXT PRIMARY KEY,dcs_id TEXT UNIQUE COLLATE NOCASE,display_name TEXT); INSERT INTO modules VALUES('module','OLD','older saved data')");
            var service = new UserDataRecoveryService(source, "1.0.0.0");
            var archive = Path.Combine(_folder, "older.zip");
            service.Backup(archive);
            Assert.Equal(1, service.Inspect(archive).DatabaseSchemaVersion);
            var target = await Create("target", "current");
            var restored = new UserDataRecoveryService(target, "1.2.3.4");
            restored.Restore(archive);
            Assert.Equal("older saved data", Name(target));
            var updated = Path.Combine(_folder, "updated.zip");
            restored.Backup(updated);
            Assert.Equal(SqliteMigrationRunner.CurrentVersion, restored.Inspect(updated).DatabaseSchemaVersion);
            Assert.Equal(1, service.Inspect(archive).DatabaseSchemaVersion);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task<string> Create(string name, string label)
        {
            var root = Path.Combine(_folder, name);
            Directory.CreateDirectory(root);
            await new SqliteMigrationRunner(Connection(root)).MigrateAsync();
            Execute(root, "INSERT INTO modules(id,dcs_id,display_name) VALUES('module','TEST','" + label + "')");
            return root;
        }
        private static string Connection(string root) => new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(root, "ipia.db"), Pooling = false }.ToString();
        private static void Execute(string root, string sql)
        {
            using var db = new SqliteConnection(Connection(root));
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        private static string Name(string root)
        {
            using var db = new SqliteConnection(Connection(root));
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT display_name FROM modules";
            return (string)command.ExecuteScalar()!;
        }
        #endregion END private helpers
    }
}
