using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks source evidence, deterministic reports and isolation of saved modules.</summary>
    public sealed class SharedUiLayerImpactReviewerTests
    {
        private const string Root = "assets/shared/ui-layer/";
        private static readonly string[] AllChanges = ["Added", "Updated", "Removed"];
        private static readonly string[] ModuleNames = ["Name A (A)", "Name D (D)", "Name N (N)", "Name Z (Z)"];
        private static readonly string[] LocalEvidence = ["Not pinned", "Different source", "Different source"];
        private static readonly string[] BaselineEvidence = ["Not pinned", "Matches baseline", "Matches baseline"];
        private static readonly string[] RenameChanges = ["Added", "Removed"];
        private static readonly string[] RenameEvidence = ["Different source", "Matches baseline"];
        #region BEGIN Public
        /// <summary>Added, updated and removed sources retain exact evidence for every saved module.</summary>
        [Fact]
        public void Classifies_source_changes_without_treating_missing_pins_as_safe_adoption()
        {
            var baseline = Defaults(new Dictionary<string, string> { [Root + "b"] = "old", [Root + "c"] = "removed" });
            var proposal = baseline with { Sources = new Dictionary<string, string> { [Root + "a"] = "added", [Root + "b"] = "new" } };
            var modules = new[] {
                Module("Z", new Dictionary<string, string> { [Root + "b"] = "old", [Root + "c"] = "removed" }),
                Module("A", new Dictionary<string, string> { [Root + "a"] = "added", [Root + "b"] = "new" }),
                Module("D", new Dictionary<string, string> { [Root + "b"] = "local", [Root + "c"] = "local removed" }),
                Module("N", null)
            };
            var report = SharedUiLayerImpactReviewer.Review(baseline, proposal, modules);
            Assert.Equal(7, report.BaselineRevision);
            Assert.Equal(AllChanges, report.Changes.Select(x => x.Change));
            Assert.Equal(new[] { Root + "a", Root + "b", Root + "c" }, report.Changes.Select(x => x.Path));
            Assert.Equal(ModuleNames, report.Modules.Select(x => x.ModuleName));
            Assert.Equal("Matches proposal", report.Modules[0].Status);
            Assert.All(report.Modules[0].Sources, x => Assert.Equal("Matches proposal", x.Status));
            Assert.Equal(LocalEvidence, report.Modules[1].Sources.Select(x => x.Status));
            Assert.All(report.Modules[2].Sources, x => Assert.Equal("Not pinned", x.Status));
            Assert.Equal(BaselineEvidence, report.Modules[3].Sources.Select(x => x.Status));
            Assert.All(report.Modules.Skip(1), x => Assert.Equal("Review required", x.Status));
            Assert.Equal("local", modules[2].Sources![Root + "b"]);
            Assert.Equal("old", baseline.Sources[Root + "b"]);
        }
        /// <summary>Unchanged drafts, empty databases and absent sources produce honest empty reports.</summary>
        [Fact]
        public void Handles_unchanged_defaults_empty_modules_and_exact_case_and_newlines()
        {
            var baseline = Defaults(new Dictionary<string, string> { [Root + "file"] = "text\n" });
            var unchanged = SharedUiLayerImpactReviewer.Review(baseline, baseline, [Module("M", null)]);
            Assert.Empty(unchanged.Changes);
            Assert.Equal("No default changes", Assert.Single(unchanged.Modules).Status);
            var proposal = baseline with { Sources = new Dictionary<string, string> { [Root + "File"] = "text\r\n" } };
            var empty = SharedUiLayerImpactReviewer.Review(baseline, proposal, []);
            Assert.Empty(empty.Modules);
            Assert.Equal(RenameChanges, empty.Changes.Select(x => x.Change));
            var module = Module("M", new Dictionary<string, string> { [Root + "File"] = "text\n", [Root + "file"] = "text\n" });
            var report = SharedUiLayerImpactReviewer.Review(baseline, proposal, [module]);
            Assert.Equal(RenameEvidence, report.Modules[0].Sources.Select(x => x.Status));
        }
        /// <summary>Reports are independent of collection insertion order, including modules with the same DCS identity.</summary>
        [Fact]
        public void Orders_equal_module_names_by_identity_and_rejects_unrelated_baselines()
        {
            var baseline = Defaults(new Dictionary<string, string> { [Root + "b"] = "same", [Root + "a"] = "old" });
            var proposal = baseline with { Sources = new Dictionary<string, string> { [Root + "a"] = "new", [Root + "b"] = "same" } };
            var first = Module("M", baseline.Sources);
            var second = Module("M", proposal.Sources);
            var one = SharedUiLayerImpactReviewer.Review(baseline, proposal, [first, second]);
            var two = SharedUiLayerImpactReviewer.Review(baseline, proposal, [second, first]);
            Assert.Equal(one.Changes, two.Changes);
            Assert.Equal(one.Modules.Select(x => x.ModuleId), two.Modules.Select(x => x.ModuleId));
            Assert.Single(one.Changes);
            Assert.Throws<ArgumentException>(() => SharedUiLayerImpactReviewer.Review(baseline, proposal with { Revision = 8 }, []));
            Assert.Throws<ArgumentException>(() => SharedUiLayerImpactReviewer.Review(baseline, proposal with { SourceCommit = new string('b', 40) }, []));
            Assert.Throws<ArgumentNullException>(() => SharedUiLayerImpactReviewer.Review(null!, proposal, []));
            Assert.Throws<ArgumentNullException>(() => SharedUiLayerImpactReviewer.Review(baseline, null!, []));
            Assert.Throws<ArgumentNullException>(() => SharedUiLayerImpactReviewer.Review(baseline, proposal, null!));
        }
        /// <summary>Real SQLite reports preserve snapshot JSON, configuration, module overrides and default history.</summary>
        [Fact]
        public async Task Reviews_reopened_modules_without_writing_or_adopting_sources()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-impact-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await defaults.SaveAsync(Defaults(new Dictionary<string, string> { [Root + "functions.json"] = "old" }) with { Revision = 0 });
                var module = Module("M", new Dictionary<string, string> { [Root + "functions.json"] = "module-local", ["config/module.json"] = "untouched" });
                await repository.SaveAsync(module);
                var before = await JsonAsync(connection);
                var saved = (await repository.GetAsync(module.Configuration.Module.Id))!;
                var draft = first with { Sources = new Dictionary<string, string> { [Root + "functions.json"] = "new" } };
                var report = SharedUiLayerImpactReviewer.Review(first, draft, [saved]);
                Assert.Equal("Different source", Assert.Single(Assert.Single(report.Modules).Sources).Status);
                Assert.Equal(before, await JsonAsync(connection));
                var second = await defaults.SaveAsync(draft);
                Assert.Equal(first.ContentSha256, (await defaults.GetAsync(1))!.ContentSha256);
                Assert.NotEqual(first.ContentSha256, second.ContentSha256);
                Assert.Equal(before, await JsonAsync(connection));
                Assert.Equal("module-local", (await repository.GetAsync(module.Configuration.Module.Id))!.Sources![Root + "functions.json"]);
                Assert.Equal("untouched", saved.Sources!["config/module.json"]);
                Assert.True(await repository.DeleteAsync(module.Configuration.Module.Id));
                Assert.Empty(SharedUiLayerImpactReviewer.Review(first, draft, []).Modules);
                Assert.Equal(2, (await defaults.GetAsync())!.Revision);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedUiLayerWorkspace Defaults(IReadOnlyDictionary<string, string> sources) => new(7, new string('a', 40), sources);
        private static EditorWorkspaceSnapshot Module(string dcsId, IReadOnlyDictionary<string, string>? sources) =>
            new(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), dcsId, "Name " + dcsId), [], [], [], "test"),
                [], [], [], [], [], [], new Dictionary<string, string> { ["custom-label"] = "preserved" }, sources);
        private static async Task<string> JsonAsync(string connectionString)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT snapshot_json FROM editor_workspaces";
            return (string)(await command.ExecuteScalarAsync())!;
        }
        #endregion END private helpers
    }
}
