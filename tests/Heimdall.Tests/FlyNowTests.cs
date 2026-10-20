using Heimdall.Application;
using Heimdall.Infrastructure;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises real SQLite activation and exact-file cleanup across service restarts.</summary>
    public sealed class FlyNowTests
    {
        #region BEGIN Public
        /// <summary>Locks other modules and clears the original recorded destination without deleting unrelated files.</summary>
        [Fact]
        public async Task Active_module_and_manifest_survive_restart_and_clear()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-fly-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var destination = Path.Combine(root, "DCS");
                Directory.CreateDirectory(destination);
                File.WriteAllText(Path.Combine(destination, "unrelated.txt"), "keep");
                var builder = new FlyNowTestPayloadBuilder();
                var service = new SqliteFlyNowService(connection, Path.Combine(root, "backups"), builder);
                var module = Guid.NewGuid();
                var active = await service.DeployAsync(module, destination);
                Assert.Equal(module, active.ModuleId);
                Assert.Contains("Config/Input/UiLayer/modifiers.lua", active.Files);
                var reopened = new SqliteFlyNowService(connection, Path.Combine(root, "backups"), builder);
                Assert.Equal(module, (await reopened.GetActiveAsync())!.ModuleId);
                await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.DeployAsync(Guid.NewGuid(), destination));
                await reopened.DeployAsync(module, destination);
                await reopened.ClearAsync();
                Assert.Null(await reopened.GetActiveAsync());
                Assert.Equal("keep", File.ReadAllText(Path.Combine(destination, "unrelated.txt")));
                Assert.All(active.Files, path => Assert.False(File.Exists(Path.Combine(destination, path))));
                await reopened.DeployAsync(Guid.NewGuid(), destination);
            }
            finally { Directory.Delete(root, true); }
        }
        /// <summary>Repeat deployment retains obsolete copied paths for exact cleanup and backups for replacements.</summary>
        [Fact]
        public Task Repeated_deployment_preserves_footprint_and_original_backups() => WithServiceAsync(async (service, destination, builder, _) =>
        {
            var target = Path.Combine(destination, "Config/Input/UiLayer/modifiers.lua");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "original");
            var module = Guid.NewGuid();
            var first = await service.DeployAsync(module, destination);
            builder.Files.Remove("KNEEBOARD/Test/page.png");
            builder.Files["KNEEBOARD/Test/new.png"] = [9];
            var second = await service.DeployAsync(module, destination);
            Assert.Contains("KNEEBOARD/Test/page.png", second.Files);
            Assert.Contains("KNEEBOARD/Test/new.png", second.Files);
            var backups = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(destination)!, "backups"), "modifiers.lua", SearchOption.AllDirectories);
            Assert.Contains(backups, x => File.ReadAllText(x) == "original");
            await service.ClearAsync();
            Assert.All(first.Files.Concat(second.Files), x => Assert.False(File.Exists(Path.Combine(destination, x))));
            Assert.Contains(backups, x => File.ReadAllText(x) == "original");
        });

        /// <summary>Invalid paths are rejected before any destination files or activation record change.</summary>
        [Theory]
        [InlineData("../outside.lua")]
        [InlineData("Config/Input/Test/../bad.lua")]
        [InlineData("Config/Input//bad.lua")]
        [InlineData("Config/Input/Test\\bad.lua")]
        [InlineData("Config/Input/Test/bad:stream")]
        [InlineData("README.TXT")]
        [InlineData("/Config/Input/Test/file.lua")]
        public Task Unsafe_paths_fail_before_copy(string path) => WithServiceAsync(async (service, destination, builder, _) =>
        {
            builder.Files[path] = [1];
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DeployAsync(Guid.NewGuid(), destination));
            Assert.Empty(Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
            Assert.Null(await service.GetActiveAsync());
        });

        /// <summary>Invalid destinations and empty payloads cannot become active.</summary>
        [Fact]
        public Task Invalid_destinations_and_empty_output_are_rejected() => WithServiceAsync(async (service, destination, builder, _) =>
        {
            await Assert.ThrowsAsync<ArgumentException>(() => service.DeployAsync(Guid.NewGuid(), "relative"));
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.DeployAsync(Guid.NewGuid(), Path.Combine(destination, "missing")));
            builder.Files.Clear();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DeployAsync(Guid.NewGuid(), destination));
            Assert.Null(await service.GetActiveAsync());
            await service.ClearAsync();
        });

        /// <summary>Changing settings cannot move the active deployment or redirect Clear.</summary>
        [Fact]
        public Task Changing_default_destination_requires_clear() => WithServiceAsync(async (service, destination, _, _) =>
        {
            var module = Guid.NewGuid();
            await service.DeployAsync(module, destination);
            var other = Path.Combine(Path.GetDirectoryName(destination)!, "DCS-other");
            Directory.CreateDirectory(other);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(module, other));
            Assert.Empty(Directory.GetFiles(other, "*", SearchOption.AllDirectories));
            await service.ClearAsync();
            Assert.Equal(other, (await service.DeployAsync(Guid.NewGuid(), other)).Destination);
        });

        /// <summary>Missing copied files or an entirely removed destination do not prevent cleanup.</summary>
        [Fact]
        public Task Missing_files_and_missing_folder_can_be_cleared() => WithServiceAsync(async (service, destination, _, _) =>
        {
            await service.DeployAsync(Guid.NewGuid(), destination);
            Directory.Delete(destination, true);
            await service.ClearAsync();
            Assert.Null(await service.GetActiveAsync());
        });

        /// <summary>Failed cleanup keeps the active lock until the obstruction is removed and Clear is retried.</summary>
        [Fact]
        public Task Failed_clear_preserves_lock_and_can_be_retried() => WithServiceAsync(async (service, destination, _, _) =>
        {
            var module = Guid.NewGuid();
            var active = await service.DeployAsync(module, destination);
            var path = Path.Combine(destination, active.Files[0]);
            File.Delete(path);
            Directory.CreateDirectory(path);
            await Assert.ThrowsAsync<IOException>(() => service.ClearAsync());
            Assert.Equal(module, (await service.GetActiveAsync())!.ModuleId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(Guid.NewGuid(), destination));
            Directory.Delete(path);
            await service.ClearAsync();
            Assert.Null(await service.GetActiveAsync());
        });

        /// <summary>Another process's lease blocks reads, deployment and cleanup without changing files.</summary>
        [Fact]
        public Task Concurrent_operations_are_rejected() => WithServiceAsync(async (service, destination, _, connection) =>
        {
            using var lease = new FileStream(new SqliteConnectionStringBuilder(connection).DataSource + ".flynow.lock",
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            await Assert.ThrowsAsync<IOException>(() => service.GetActiveAsync());
            await Assert.ThrowsAsync<IOException>(() => service.DeployAsync(Guid.NewGuid(), destination));
            await Assert.ThrowsAsync<IOException>(() => service.ClearAsync());
            Assert.Empty(Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
        });

        /// <summary>Links cannot redirect deployment or cleanup outside the recorded destination.</summary>
        [Fact]
        public Task Linked_paths_are_rejected() => WithServiceAsync(async (service, destination, _, _) =>
        {
            if (OperatingSystem.IsWindows()) return; // Windows link creation requires developer-mode privileges.
            var outside = Path.Combine(Path.GetDirectoryName(destination)!, "outside");
            Directory.CreateDirectory(outside);
            Directory.CreateSymbolicLink(Path.Combine(destination, "Config"), outside);
            await Assert.ThrowsAsync<IOException>(() => service.DeployAsync(Guid.NewGuid(), destination));
            Assert.Empty(Directory.GetFiles(outside, "*", SearchOption.AllDirectories));
        });

        /// <summary>A durable interrupted-copy intent restores replacements and removes new copied files on restart.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Interrupted_copy_rolls_back_to_previous_activation(bool hadPrevious) => WithServiceAsync(async (service, destination, _, connection) =>
        {
            var previous = hadPrevious ? await service.DeployAsync(Guid.NewGuid(), destination) : null;
            const string originalPath = "Config/Input/UiLayer/modifiers.lua";
            const string newPath = "KNEEBOARD/Test/interrupted.png";
            var operation = Path.Combine(Path.GetDirectoryName(destination)!, "backups", "interrupted");
            var original = Path.Combine(operation, "original", originalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            File.WriteAllText(original, "before-copy");
            var target = Path.Combine(destination, originalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "partial-copy");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(destination, newPath))!);
            File.WriteAllText(Path.Combine(destination, newPath), "partial-copy");
            var candidate = new FlyNowDeployment(previous?.ModuleId ?? Guid.NewGuid(), "Test", destination, [originalPath, newPath]);
            await WriteJournalAsync(connection, new
            {
                Active = candidate,
                Previous = previous,
                BackupFolder = operation,
                ReplacedFiles = new Dictionary<string, bool> { [originalPath] = true, [newPath] = false },
                Writing = true
            });
            var restored = await service.GetActiveAsync();
            Assert.Equal(previous?.ModuleId, restored?.ModuleId);
            Assert.Equal("before-copy", File.ReadAllText(target));
            Assert.False(File.Exists(Path.Combine(destination, newPath)));
        });

        /// <summary>Incomplete cleanup blocks repeat deployment and tolerates previously removed manifest files.</summary>
        [Fact]
        public Task Persisted_partial_clear_keeps_lock_until_retry() => WithServiceAsync(async (service, destination, _, connection) =>
        {
            var active = await service.DeployAsync(Guid.NewGuid(), destination);
            File.Delete(Path.Combine(destination, active.Files[0]));
            await WriteJournalAsync(connection, new
            {
                Active = active with { Clearing = true },
                Previous = (FlyNowDeployment?)null,
                BackupFolder = "",
                ReplacedFiles = new Dictionary<string, bool>(),
                Writing = false
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(active.ModuleId, destination));
            Assert.True((await service.GetActiveAsync())!.Clearing);
            await service.ClearAsync();
            Assert.Null(await service.GetActiveAsync());
        });
        #endregion END Public

        #region BEGIN private helpers
        private static async Task WithServiceAsync(Func<SqliteFlyNowService, string, FlyNowTestPayloadBuilder, string, Task> test)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-fly-test-" + Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(root, "DCS");
            Directory.CreateDirectory(destination);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var builder = new FlyNowTestPayloadBuilder();
                await test(new SqliteFlyNowService(connection, Path.Combine(root, "backups"), builder), destination, builder, connection);
            }
            finally { Directory.Delete(root, true); }
        }

        private static async Task WriteJournalAsync(string connection, object journal)
        {
            await using var db = new SqliteConnection(connection);
            await db.OpenAsync();
            using var command = db.CreateCommand();
            command.CommandText = "INSERT INTO fly_now_deployment VALUES(1,$json) ON CONFLICT(id) DO UPDATE SET journal_json=excluded.journal_json";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(journal));
            await command.ExecuteNonQueryAsync();
        }
        #endregion END private helpers
    }
}
