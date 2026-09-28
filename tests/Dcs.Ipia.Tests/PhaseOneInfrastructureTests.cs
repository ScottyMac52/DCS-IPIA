using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Tests.Fakes;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Tests
{
    /// <summary>Represents phase one infrastructure tests.</summary>
    public sealed class PhaseOneInfrastructureTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"dcs-ipia-phase1-{Guid.NewGuid():N}.db");
        private string Connection => $"Data Source={path};Pooling=False";
        #region BEGIN Public
        /// <summary>real SQLite round trips and replaces configuration.</summary>
        /// <returns>A task that completes when the operation finishes</returns>
        [Fact]
        public async Task Real_sqlite_round_trips_and_replaces_configuration()
        {
            await new SqliteMigrationRunner(Connection).MigrateAsync();
            await new SqliteMigrationRunner(Connection).MigrateAsync();
            var repository = new SqliteConfigurationRepository(Connection);
            var value = Fixture("first");
            Assert.Null(await repository.GetAsync(value.Module.Id));
            await repository.ReplaceAsync(value);
            Assert.Equal(
                ConfigurationSnapshotSerializer.Serialize([value]).Content,
                ConfigurationSnapshotSerializer
                    .Serialize([await repository.GetAsync(value.Module.Id) ?? throw new InvalidOperationException()])
                    .Content);
            Assert.Single(await repository.ListAsync());
            var replacement = Fixture("second");
            await repository.ReplaceAsync(replacement);
            var restored = await repository.GetAsync(value.Module.Id);
            Assert.Equal("second", restored!.Provenance);
            Assert.Single(restored.Bindings);
            Assert.True(restored.Bindings[0].Filter!.Invert is true);
            Assert.DoesNotContain(await repository.ListAsync(), x => x.Module.DcsId != "UH-1H");
        }
        /// <summary>v1 database migrates transactionally.</summary>
        /// <returns>A task that completes when the operation finishes</returns>
        [Fact]
        public async Task V1_database_migrates_transactionally()
        {
            await using (var c = new SqliteConnection(Connection))
            {
                await c.OpenAsync();
                await using var cmd = c.CreateCommand();
                cmd.CommandText =
                    "CREATE TABLE schema_migrations(version INTEGER NOT NULL PRIMARY KEY,applied_utc TEXT NOT NULL);CREATE TABLE modules(id TEXT NOT NULL PRIMARY KEY,dcs_id TEXT NOT NULL UNIQUE COLLATE NOCASE,display_name TEXT NOT NULL);INSERT INTO schema_migrations VALUES(1,'now');";
                await cmd.ExecuteNonQueryAsync();
            }
            await new SqliteMigrationRunner(Connection).MigrateAsync();
            await using (var migrated = new SqliteConnection(Connection))
            {
                await migrated.OpenAsync();
                await using var check = migrated.CreateCommand();
                check.CommandText = "SELECT COUNT(*) FROM schema_migrations";
                Assert.Equal(4L, await check.ExecuteScalarAsync());
            }
        }
        /// <summary>Deletes temporary test data.</summary>
        public void Dispose()
        {
            // Windows may briefly retain the SQLite file after the final native statement closes.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    File.Delete(path);
                    break;
                }
                catch (IOException) when (attempt < 9)
                {
                    Thread.Sleep(50);
                }
            }
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ModuleConfiguration Fixture(string provenance)
        {
            var module = new DcsModule(Guid.Parse("11111111-1111-1111-1111-111111111111"), "UH-1H", "Huey");
            var profile = new DeviceProfile(Guid.Parse("22222222-2222-2222-2222-222222222222"), "moza-ab9-hornet-grip",
                                            null, "cyclic", "MOZA.diff.lua");
            var modifier = new DcsModifier("SHIFT", "MOZA", "JOY_BTN3", ModifierMode.Hold);
            var binding = new DcsBinding(profile.Id, BindingSection.Axis, "a1", "Roll", BindingDisposition.Added,
                                         "JOY_X", ["SHIFT"], new(Invert: true, Curvature: [-.1, .1]));
            return new(module, [profile], [modifier], [binding], provenance);
        }
        #endregion END private helpers
    }
}
