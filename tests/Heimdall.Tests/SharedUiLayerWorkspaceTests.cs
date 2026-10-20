using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises shared-default seeding, immutable history, concurrency, and module isolation with SQLite.</summary>
    public sealed class SharedUiLayerWorkspaceTests
    {
        #region BEGIN Public
        /// <summary>Defaults reopen with exact sources and history while existing module state stays unchanged.</summary>
        [Fact]
        public async Task Saves_and_reopens_independent_default_revisions()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-defaults-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                var migration = new SqliteMigrationRunner(connection);
                await migration.MigrateAsync();
                await migration.MigrateAsync();
                var modules = new SqliteEditorWorkspaceRepository(connection);
                var module = new EditorWorkspaceSnapshot(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "TEST", "Test"), [], [], [], "test"),
                    [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { ["module-override"] = "unchanged" });
                await modules.SaveAsync(module);
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                Assert.Null(await repository.GetAsync());
                var seed = await SharedUiLayerWorkspaceFactory.CreateAsync();
                Assert.Equal(0, seed.Revision);
                Assert.Contains("assets/shared/ui-layer/functions.json", seed.Sources.Keys);
                Assert.Contains("assets/shared/ui-layer/input/UiLayer/keyboard/Keyboard.diff.lua", seed.Sources.Keys);
                var first = await repository.SaveAsync(seed);
                Assert.Equal(1, first.Revision);
                var reopened = (await new SqliteSharedUiLayerWorkspaceRepository(connection).GetAsync())!;
                Assert.Equal(first.ContentSha256, reopened.ContentSha256);
                Assert.Equal(seed.Sources.OrderBy(x => x.Key), reopened.Sources.OrderBy(x => x.Key));
                var changed = new Dictionary<string, string>(reopened.Sources);
                changed["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] += "\n-- next revision\n";
                var second = await repository.SaveAsync(reopened with { Sources = changed });
                Assert.Equal(2, second.Revision);
                Assert.NotEqual(first.ContentSha256, second.ContentSha256);
                Assert.Equal(first.ContentSha256, (await repository.GetAsync(1))!.ContentSha256);
                Assert.Null(await repository.GetAsync(99));
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(first));
                Assert.Equal(2, (await repository.GetAsync())!.Revision);
                Assert.Equal("unchanged", (await modules.GetAsync(module.Configuration.Module.Id))!.Sources!["module-override"]);
                Assert.Single(await modules.ListAsync());
                await using var database = new SqliteConnection(connection);
                await database.OpenAsync();
                await using var tamper = database.CreateCommand();
                tamper.CommandText = "UPDATE shared_ui_layer_revisions SET sources_json='{}' WHERE revision=2";
                await tamper.ExecuteNonQueryAsync();
                await Assert.ThrowsAsync<InvalidDataException>(() => repository.GetAsync());
                Assert.NotNull(await repository.GetAsync(1));
            }
            finally { File.Delete(file); }
        }
        /// <summary>Invalid source identities and paths are rejected without creating defaults.</summary>
        /// <param name="scenario">Invalid input to supply.</param>
        [Theory]
        [InlineData("revision")]
        [InlineData("commit")]
        [InlineData("empty")]
        [InlineData("path")]
        [InlineData("text")]
        public async Task Rejects_invalid_default_sources(string scenario)
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-defaults-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var sources = new Dictionary<string, string> { [scenario == "path" ? "assets/shared/ui-layer/../outside" : "assets/shared/ui-layer/functions.json"] = scenario == "text" ? " " : "{}" };
                var workspace = new SharedUiLayerWorkspace(scenario == "revision" ? -1 : 0, scenario == "commit" ? "invalid" : new string('a', 40), scenario == "empty" ? new Dictionary<string, string>() : sources);
                await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(workspace));
                Assert.Null(await repository.GetAsync());
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        #endregion END private helpers
    }
}
