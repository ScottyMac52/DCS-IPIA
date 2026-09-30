using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks complete, isolated, and atomic deletion of local module data.</summary>
    public sealed class SqliteModuleDeletionTests
    {
        #region BEGIN Public
        /// <summary>Deleting a module removes all related rows and permits a clean import with the same DCS ID.</summary>
        /// <returns>Completion of the database deletion and fresh import checks.</returns>
        [Fact]
        public async Task DeletesAllModuleDataAndAllowsFreshImport()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-delete-" + Guid.NewGuid().ToString("N") + ".db");
            var connectionString = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connectionString);
                var deleted = Workspace("UH-1H");
                var retained = Workspace("F-14BU");
                await repository.SaveAsync(deleted);
                await repository.SaveAsync(retained);
                await using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();
                await ExecuteAsync(connection, "INSERT INTO app_preferences VALUES(1,1,'Dark','Ask'); " +
                    "INSERT INTO shared_ui_layer_revisions VALUES(1,'shared','{}','hash');");
                var retainedJson = EditorSnapshotSerializer.Serialize((await repository.GetAsync(retained.Configuration.Module.Id))!);
                Assert.True(await repository.DeleteAsync(deleted.Configuration.Module.Id));
                foreach (var table in new[] { "modules", "device_profiles", "modifiers", "bindings", "editor_workspaces" })
                {
                    await using var command = connection.CreateCommand();
                    var column = table == "modules" ? "id" : "module_id";
                    command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column}=$id";
                    command.Parameters.AddWithValue("$id", deleted.Configuration.Module.Id.ToString("D"));
                    Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
                }
                Assert.Null(await repository.GetAsync(deleted.Configuration.Module.Id));
                Assert.Null(await new SqliteConfigurationRepository(connectionString).GetAsync(deleted.Configuration.Module.Id));
                Assert.Equal(retained.Configuration.Module.Id, Assert.Single(await repository.ListAsync()));
                Assert.Equal(retainedJson, EditorSnapshotSerializer.Serialize((await repository.GetAsync(retained.Configuration.Module.Id))!));
                foreach (var table in new[] { "app_preferences", "shared_ui_layer_revisions" })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT COUNT(*) FROM {table}";
                    Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
                }
                var fresh = Workspace("UH-1H") with { Assignments = [], Labels = new Dictionary<string, string>(),
                    Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = "fresh import" } };
                await repository.SaveAsync(fresh);
                var reopened = (await repository.GetAsync(fresh.Configuration.Module.Id))!;
                Assert.NotEqual(deleted.Configuration.Module.Id, reopened.Configuration.Module.Id);
                Assert.Empty(reopened.Assignments);
                Assert.Empty(reopened.Labels);
                Assert.Equal("fresh import", reopened.Sources!["config/kneeboard.json"]);
                Assert.Equal(2, (await repository.ListAsync()).Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>A failed cascade leaves the complete module intact, and repeat deletion is harmless.</summary>
        /// <returns>Completion of rollback, missing identity, and repeat deletion checks.</returns>
        [Fact]
        public async Task RollsBackFailuresAndHandlesMissingModules()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-delete-rollback-" + Guid.NewGuid().ToString("N") + ".db");
            var connectionString = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connectionString);
                var workspace = Workspace("UH-1H");
                await repository.SaveAsync(workspace);
                var original = EditorSnapshotSerializer.Serialize((await repository.GetAsync(workspace.Configuration.Module.Id))!);
                await using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();
                await ExecuteAsync(connection, "CREATE TRIGGER reject_binding_delete BEFORE DELETE ON bindings " +
                    "BEGIN SELECT RAISE(ABORT,'test cascade failure'); END;");
                await Assert.ThrowsAsync<SqliteException>(() => repository.DeleteAsync(workspace.Configuration.Module.Id));
                Assert.Equal(original, EditorSnapshotSerializer.Serialize((await repository.GetAsync(workspace.Configuration.Module.Id))!));
                Assert.False(await repository.DeleteAsync(Guid.NewGuid()));
                await Assert.ThrowsAsync<ArgumentException>(() => repository.DeleteAsync(Guid.Empty));
                await ExecuteAsync(connection, "DROP TRIGGER reject_binding_delete;");
                Assert.True(await repository.DeleteAsync(workspace.Configuration.Module.Id));
                Assert.False(await repository.DeleteAsync(workspace.Configuration.Module.Id));
                Assert.Empty(await repository.ListAsync());
            }
            finally
            {
                File.Delete(path);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot Workspace(string dcsId)
        {
            var module = new DcsModule(Guid.NewGuid(), dcsId, dcsId);
            var profile = new DeviceProfile(Guid.NewGuid(), "stick", null, "Stick", "Stick.diff.lua");
            var binding = new DcsBinding(profile.Id, BindingSection.Key, "FIRE", "Fire", BindingDisposition.Added, "JOY_BTN1");
            var config = new ModuleConfiguration(module, [profile],
                [new DcsModifier("SHIFT", "Stick", "JOY_BTN2", ModifierMode.Hold)], [binding], "import");
            var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            return new(config, [], [], [], [new CommandPaletteEntry("FIRE", "Fire", "Weapons", false)], [],
                [new DraftAssignment(slot, new DraftCommand("FIRE", "Fire", false, AssignmentLayer.Module))],
                new Dictionary<string, string> { [slot.Key] = "Edited label" },
                new Dictionary<string, string> { ["config/kneeboard.json"] = "old import" });
        }
        private static async Task ExecuteAsync(SqliteConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        #endregion END private helpers
    }
}
