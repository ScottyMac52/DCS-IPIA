using Dcs.Ipia.Application;
using Dcs.Ipia.Tests.Fakes;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Packaging;
using Dcs.Ipia.Rendering;
using Microsoft.Data.Sqlite;
using Xunit;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Tests
{
    /// <summary>Represents infrastructure tests.</summary>
    public sealed class InfrastructureTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"dcs-ipia-{Guid.NewGuid():N}.db");
        private string Connection => $"Data Source={path};Pooling=False";
        #region BEGIN Public
        /// <summary>migration and crud use real SQLite.</summary>
        /// <returns>A task that completes when the operation finishes</returns>
        [Fact]
        public async Task Migration_and_crud_use_real_sqlite()
        {
            var migration = new SqliteMigrationRunner(Connection);
            await migration.MigrateAsync();
            await migration.MigrateAsync();
            var repo = new SqliteModuleRepository(Connection);
            var id = Guid.NewGuid();
            Assert.Null(await repo.GetAsync(id));
            await repo.UpsertAsync(new DcsModule(id, "UH-1H", "Huey"));
            await repo.UpsertAsync(new DcsModule(id, "UH-1H", "Huey updated"));
            Assert.Equal("Huey updated", (await repo.GetAsync(id))!.DisplayName);
            Assert.Single(await repo.ListAsync());
            Assert.True(await repo.DeleteAsync(id));
            Assert.False(await repo.DeleteAsync(id));
        }
        /// <summary>backup is verified and readable.</summary>
        /// <returns>A task that completes when the operation finishes</returns>
        [Fact]
        public async Task Backup_is_verified_and_readable()
        {
            await new SqliteMigrationRunner(Connection).MigrateAsync();
            var backup = path + ".backup";
            try
            {
                new SqliteBackupService(Connection).Backup(backup);
                await using var db = new SqliteConnection($"Data Source={backup};Pooling=False");
                await db.OpenAsync();
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM schema_migrations";
                Assert.Equal(5L, await cmd.ExecuteScalarAsync());
            }
            finally
            {
                File.Delete(backup);
            }
        }
        /// <summary>empty backup path is rejected.</summary>
        [Fact]
        public void Empty_backup_path_is_rejected() =>
            Assert.Throws<ArgumentException>(() => new SqliteBackupService(Connection).Backup(""));
        /// <summary>Deletes temporary test data.</summary>
        public void Dispose()
        {
            File.Delete(path);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
    }
}
