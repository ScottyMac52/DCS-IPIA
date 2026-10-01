using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies portable baseline references, old snapshot compatibility and SQLite isolation.</summary>
    public sealed class SharedUiLayerRevisionReferenceTests
    {
        private const string PathKey = "assets/shared/ui-layer/functions.json";
        #region BEGIN Public
        /// <summary>Capturing a baseline copies its sources and preserves all effective module material.</summary>
        [Fact]
        public void Freezes_reference_without_replacing_module_edits()
        {
            var sources = new Dictionary<string, string> { [PathKey] = "defaults" };
            var revision = Saved(sources);
            var module = Module();
            var captured = SharedUiLayerRevisionReference.Attach(module, revision);
            Assert.Same(module.Sources, captured.Sources);
            Assert.Same(module.Assignments, captured.Assignments);
            Assert.Same(module.Labels, captured.Labels);
            sources[PathKey] = "later mutation";
            Assert.Equal("defaults", captured.SharedDefault!.Sources[PathKey]);
            Assert.Equal(revision.ContentSha256, captured.SharedDefault.ContentSha256);
            Assert.Throws<ArgumentNullException>(() => SharedUiLayerRevisionReference.Freeze(null!));
            Assert.Throws<ArgumentNullException>(() => SharedUiLayerRevisionReference.Attach(null!, revision));
        }
        /// <summary>Unsigned drafts, invalid provenance and changed reference content fail verification.</summary>
        /// <param name="scenario">Invalid revision evidence.</param>
        [Theory]
        [InlineData("revision")]
        [InlineData("commit")]
        [InlineData("empty")]
        [InlineData("path")]
        [InlineData("blank")]
        [InlineData("hash")]
        public void Rejects_invalid_reference(string scenario)
        {
            var valid = Saved(new Dictionary<string, string> { [PathKey] = "defaults" });
            var invalid = scenario switch {
                "revision" => valid with { Revision = 0 },
                "commit" => valid with { SourceCommit = "bad" },
                "empty" => valid with { Sources = new Dictionary<string, string>() },
                "path" => valid with { Sources = new Dictionary<string, string> { ["assets/shared/ui-layer/../outside"] = "text" } },
                "blank" => valid with { Sources = new Dictionary<string, string> { [PathKey] = " " } },
                _ => valid with { ContentSha256 = new string('0', 64) }
            };
            Assert.Throws<InvalidDataException>(() => SharedUiLayerRevisionReference.Freeze(invalid));
            Assert.Throws<InvalidDataException>(() => EditorSnapshotSerializer.Serialize(Module() with { SharedDefault = invalid }));
        }
        /// <summary>SQLite and frozen exports retain baseline identity independently of later defaults or an empty restore DB.</summary>
        [Fact]
        public async Task Reopens_exports_and_restores_reference_without_external_default_history()
        {
            var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ipia-reference-" + Guid.NewGuid().ToString("N") + ".db");
            var restoredFile = file + ".restored";
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await defaults.SaveAsync(new(0, new string('a', 40), new Dictionary<string, string> { [PathKey] = "defaults" }));
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var original = Module();
                await repository.SaveAsync(original);
                var oldExport = EditorSnapshotSerializer.Serialize(original);
                Assert.Null(EditorSnapshotSerializer.Deserialize(oldExport).SharedDefault);
                var pinned = SharedUiLayerRevisionReference.Attach(original, first);
                await repository.SaveAsync(pinned);
                var reopened = (await repository.GetAsync(original.Configuration.Module.Id))!;
                Assert.Equal(first.ContentSha256, reopened.SharedDefault!.ContentSha256);
                Assert.Equal("module override", reopened.Sources![PathKey]);
                var frozen = EditorSnapshotSerializer.Serialize(reopened);
                var restored = EditorSnapshotSerializer.Deserialize(frozen);
                Assert.Equal(frozen.Content, EditorSnapshotSerializer.Serialize(restored).Content);
                Assert.Equal(first.ContentSha256, restored.SharedDefault!.ContentSha256);
                var next = await defaults.SaveAsync(first with { Sources = new Dictionary<string, string> { [PathKey] = "new defaults" } });
                Assert.Equal(1, (await repository.GetAsync(original.Configuration.Module.Id))!.SharedDefault!.Revision);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(original));
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(reopened with { SharedDefault = next }));
                Assert.Equal(first.ContentSha256, (await repository.GetAsync(original.Configuration.Module.Id))!.SharedDefault!.ContentSha256);
                var restoreConnection = "Data Source=" + restoredFile + ";Pooling=False";
                await new SqliteMigrationRunner(restoreConnection).MigrateAsync();
                await new SqliteEditorWorkspaceRepository(restoreConnection).SaveAsync(restored);
                var emptyDbRestore = (await new SqliteEditorWorkspaceRepository(restoreConnection).GetAsync(original.Configuration.Module.Id))!;
                Assert.Equal(frozen.Content, EditorSnapshotSerializer.Serialize(emptyDbRestore).Content);
                Assert.Null(await new SqliteSharedUiLayerWorkspaceRepository(restoreConnection).GetAsync());
                Assert.True(await repository.DeleteAsync(original.Configuration.Module.Id));
                Assert.NotNull(await defaults.GetAsync(1));
                await using var database = new SqliteConnection(restoreConnection);
                await database.OpenAsync();
                await using var tamper = database.CreateCommand();
                tamper.CommandText = "UPDATE editor_workspaces SET snapshot_json=replace(snapshot_json,'defaults','changed')";
                await tamper.ExecuteNonQueryAsync();
                await Assert.ThrowsAsync<InvalidDataException>(() => new SqliteEditorWorkspaceRepository(restoreConnection).GetAsync(original.Configuration.Module.Id));
            }
            finally { File.Delete(file); File.Delete(restoredFile); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedUiLayerWorkspace Saved(IReadOnlyDictionary<string, string> sources)
        {
            var sorted = new SortedDictionary<string, string>(sources.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal);
            var commit = new string('a', 40);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit + "\n" + JsonSerializer.Serialize(sorted))));
            return new(1, commit, sources, hash);
        }
        private static EditorWorkspaceSnapshot Module() =>
            new(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "REFERENCE", "Reference"), [], [], [], "test"),
                [], [], [], [], [], [], new Dictionary<string, string> { ["label"] = "authored" },
                new Dictionary<string, string> { [PathKey] = "module override" });
        #endregion END private helpers
    }
}

