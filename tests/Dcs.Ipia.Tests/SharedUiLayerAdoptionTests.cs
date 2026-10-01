using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks explicit adoption choices, concurrency, rollback and cross-module preservation.</summary>
    public sealed class SharedUiLayerAdoptionTests
    {
        private const string Root = "assets/shared/ui-layer/";
        #region BEGIN Public
        /// <summary>Only selected source changes are adopted; local differences and module-owned edits stay isolated.</summary>
        [Fact]
        public async Task Reviews_and_applies_only_explicit_source_decisions()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-adopt-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await defaults.SaveAsync(new(0, new string('a', 40), new Dictionary<string, string> {
                    [Root + "a"] = "old", [Root + "b"] = "old", [Root + "deleted"] = "old", [Root + "missing"] = "old" }));
                var second = await defaults.SaveAsync(first with { Sources = new Dictionary<string, string> {
                    [Root + "a"] = "new", [Root + "b"] = "new", [Root + "added"] = "new", [Root + "missing"] = "new" } });
                var module = SharedUiLayerRevisionReference.Attach(Module("A"), first) with {
                    Sources = new Dictionary<string, string> {
                        [Root + "a"] = "old", [Root + "b"] = "local", [Root + "deleted"] = "old",
                        [Root + "added"] = "local new source", ["config/overlay"] = "module-owned" } };
                var review = SharedUiLayerAdoption.Review(module, second);
                Assert.False(review.Single(x => x.Path == Root + "a").Conflict);
                Assert.False(review.Single(x => x.Path == Root + "deleted").Conflict);
                Assert.True(review.Single(x => x.Path == Root + "b").Conflict);
                Assert.True(review.Single(x => x.Path == Root + "missing").Conflict);
                Assert.True(review.Single(x => x.Path == Root + "added").Conflict);
                var updated = SharedUiLayerAdoption.Apply(module, second, [Root + "a", Root + "deleted"]);
                Assert.Equal("new", updated.Sources![Root + "a"]);
                Assert.Equal("local", updated.Sources[Root + "b"]);
                Assert.Equal("local new source", updated.Sources[Root + "added"]);
                Assert.False(updated.Sources.ContainsKey(Root + "missing"));
                Assert.False(updated.Sources.ContainsKey(Root + "deleted"));
                Assert.Equal("module-owned", updated.Sources["config/overlay"]);
                Assert.Same(module.Assignments, updated.Assignments);
                Assert.Same(module.Labels, updated.Labels);
                Assert.Same(module.Configuration, updated.Configuration);
                Assert.Equal(second.ContentSha256, updated.SharedDefault!.ContentSha256);
                var explicitConflict = SharedUiLayerAdoption.Apply(module, second, [Root + "b", Root + "added"]);
                Assert.Equal("new", explicitConflict.Sources![Root + "b"]);
                Assert.Equal("new", explicitConflict.Sources[Root + "added"]);
                Assert.Equal("old", module.Sources![Root + "a"]);
                Assert.Throws<ArgumentException>(() => SharedUiLayerAdoption.Apply(module, second, ["config/overlay"]));
                Assert.Throws<ArgumentNullException>(() => SharedUiLayerAdoption.Apply(module, second, null!));
                Assert.Throws<ArgumentNullException>(() => SharedUiLayerAdoption.Review(null!, second));
                Assert.Throws<InvalidOperationException>(() => SharedUiLayerAdoption.Review(Module("B"), second));
                Assert.Throws<InvalidOperationException>(() => SharedUiLayerAdoption.Review(updated, first));
                Assert.Throws<InvalidOperationException>(() => SharedUiLayerAdoption.Review(module, second with { Revision = 1 }));
                var none = SharedUiLayerAdoption.Apply(module, second, []);
                Assert.Equal(module.Sources.OrderBy(x => x.Key), none.Sources!.OrderBy(x => x.Key));
                Assert.Empty(SharedUiLayerAdoption.Review(none, second));
            }
            finally { File.Delete(file); }
        }
        /// <summary>Reviewed saves detect payload and configuration edits, preserve history and restore prior bytes.</summary>
        [Fact]
        public async Task Saves_atomically_with_stale_rejection_and_verified_rollback()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-adopt-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var baseline = await defaults.SaveAsync(new(0, new string('a', 40), new Dictionary<string, string> { [Root + "functions.json"] = "old" }));
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var module = Module("A");
                var other = Module("B");
                await repository.SaveAsync(module);
                await repository.SaveAsync(other);
                var expected = (await repository.GetAsync(module.Configuration.Module.Id))!;
                Assert.Null(await repository.GetPriorAsync(module.Configuration.Module.Id));
                var recorded = SharedUiLayerRevisionReference.Attach(expected, baseline);
                await repository.SaveIfUnchangedAsync(expected, recorded);
                var prior = (await repository.GetPriorAsync(module.Configuration.Module.Id))!;
                Assert.Equal(EditorSnapshotSerializer.Serialize(expected).Content, EditorSnapshotSerializer.Serialize(prior).Content);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveIfUnchangedAsync(expected, recorded));
                var current = (await repository.GetAsync(module.Configuration.Module.Id))!;
                var configEdit = current with { Configuration = Renamed(current.Configuration, "A", "Changed name") };
                await repository.SaveAsync(configEdit);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveIfUnchangedAsync(current, current));
                var latest = (await repository.GetAsync(module.Configuration.Module.Id))!;
                await Assert.ThrowsAsync<SqliteException>(() => repository.SaveIfUnchangedAsync(latest, latest with {
                    Configuration = Renamed(latest.Configuration, "B", latest.Configuration.Module.DisplayName) }));
                Assert.Equal(EditorSnapshotSerializer.Serialize(prior).Content,
                    EditorSnapshotSerializer.Serialize((await repository.GetPriorAsync(module.Configuration.Module.Id))!).Content);
                await repository.SaveIfUnchangedAsync(latest, prior);
                Assert.Equal(EditorSnapshotSerializer.Serialize(expected).Content,
                    EditorSnapshotSerializer.Serialize((await repository.GetAsync(module.Configuration.Module.Id))!).Content);
                Assert.Equal(EditorSnapshotSerializer.Serialize(other).Content,
                    EditorSnapshotSerializer.Serialize((await repository.GetAsync(other.Configuration.Module.Id))!).Content);
                Assert.Equal(baseline.ContentSha256, (await defaults.GetAsync())!.ContentSha256);
                await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveIfUnchangedAsync(null!, prior));
                await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveIfUnchangedAsync(prior, null!));
                await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveIfUnchangedAsync(prior, other));
                Assert.True(await repository.DeleteAsync(module.Configuration.Module.Id));
                Assert.Null(await repository.GetPriorAsync(module.Configuration.Module.Id));
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveIfUnchangedAsync(prior, prior));
            }
            finally { File.Delete(file); }
        }
        /// <summary>Rollback history rejects corrupt bytes rather than restoring unverified module state.</summary>
        [Fact]
        public async Task Rejects_corrupt_history()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-adopt-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var module = Module("A");
                await repository.SaveAsync(module);
                var expected = (await repository.GetAsync(module.Configuration.Module.Id))!;
                await repository.SaveIfUnchangedAsync(expected, expected with { Sources = new Dictionary<string, string> { ["config/overlay"] = "new" } });
                await using var database = new SqliteConnection(connection);
                await database.OpenAsync();
                await using var tamper = database.CreateCommand();
                tamper.CommandText = "UPDATE module_update_history SET snapshot_json='{}'";
                await tamper.ExecuteNonQueryAsync();
                await Assert.ThrowsAsync<InvalidDataException>(() => repository.GetPriorAsync(module.Configuration.Module.Id));
                Assert.Equal("new", (await repository.GetAsync(module.Configuration.Module.Id))!.Sources!["config/overlay"]);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ModuleConfiguration Renamed(ModuleConfiguration configuration, string dcsId, string displayName) =>
            new(new DcsModule(configuration.Module.Id, dcsId, displayName), configuration.Profiles,
                configuration.Modifiers, configuration.Bindings, configuration.Provenance);
        private static EditorWorkspaceSnapshot Module(string id)
        {
            var module = new DcsModule(Guid.NewGuid(), id, "Module " + id);
            var profile = new DeviceProfile(Guid.NewGuid(), "test-stick", null, "Stick", "Test stick.diff.lua");
            var modifier = new DcsModifier("SHIFT", "Keyboard", "LShift", ModifierMode.Hold);
            var filter = new AxisFilter(Deadzone: 3, Curvature: [1, 2, 3], Invert: true);
            var configuration = new ModuleConfiguration(module, [profile], [modifier], [
                new DcsBinding(profile.Id, BindingSection.Key, "key", "Key", BindingDisposition.Added, "JOY_BTN1", ["SHIFT"]),
                new DcsBinding(profile.Id, BindingSection.Axis, "axis", "Axis", BindingDisposition.Added, "JOY_X", [], filter),
                new DcsBinding(profile.Id, BindingSection.Key, "history", "History", BindingDisposition.Removed, "JOY_BTN2")
            ], "test");
            var assignment = new DraftAssignment(
                new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X", ""),
                new("axis", "Axis", true, AssignmentLayer.Module)) { Filter = filter };
            return new(configuration, [], [], [], [], [], [assignment],
                new Dictionary<string, string> { [assignment.Slot.Key] = "authored" },
                new Dictionary<string, string> { ["config/overlay"] = "module-owned" });
        }
        #endregion END private helpers
    }
}
