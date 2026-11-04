using System.Text.Json.Nodes;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks command catalog drafts, binding protection, source preservation, and independent SQLite revisions.</summary>
    public sealed class SharedUiLayerCommandEditorTests
    {
        private const string Catalog = "assets/shared/ui-layer/functions.json";
        private const string Profile = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua";
        #region BEGIN Public
        /// <summary>Catalog changes retain unknown metadata, Lua, identities, and imported command choices.</summary>
        [Fact]
        public void StagesCatalogEditsWithoutChangingBindings()
        {
            var baseline = Workspace();
            var old = DcsSharedUiLayerCommandEditor.Commands(baseline).Single(x => x.Id == "bound");
            var updated = DcsSharedUiLayerCommandEditor.Set(baseline, old, old with { Label = "New display name", Category = "VR" });
            Assert.Equal("New display name", DcsSharedUiLayerBindingEditor.Commands(updated).Single(x => x.Id == "BOUND").Name);
            Assert.Contains(DcsSharedUiLayerBindingEditor.Commands(updated), x => x.Id == "IMPORTED");
            var document = JsonNode.Parse(updated.Sources[Catalog])!;
            Assert.Equal("root metadata", document["extension"]!.GetValue<string>());
            Assert.Equal("entry metadata", document["functions"]![0]!["extension"]!.GetValue<string>());
            Assert.Equal(baseline.Sources.Where(x => x.Key != Catalog), updated.Sources.Where(x => x.Key != Catalog));
            Assert.Equal(baseline.Revision, updated.Revision);
            Assert.Equal(baseline.SourceCommit, updated.SourceCommit);
            Assert.Null(updated.ContentSha256);
            Assert.Equal("Bound name", DcsSharedUiLayerCommandEditor.Commands(baseline)[0].Label);
            var added = new DcsSharedUiLayerCommand("custom", "CUSTOM", "Custom action", "General");
            updated = DcsSharedUiLayerCommandEditor.Set(updated, null, added);
            updated = DcsSharedUiLayerCommandEditor.Set(updated, added, added with { Command = "CUSTOM2" });
            var current = DcsSharedUiLayerCommandEditor.Commands(updated).Single(x => x.Id == "custom");
            updated = DcsSharedUiLayerCommandEditor.Remove(updated, current);
            Assert.DoesNotContain(DcsSharedUiLayerCommandEditor.Commands(updated), x => x.Id == "custom");
            DcsSharedUiLayerBindingEditor.Validate(updated);
        }
        /// <summary>Active keys and axes in any profile prevent deletion or code changes; removal history survives catalog deletion.</summary>
        [Fact]
        public void ProtectsAssignmentsAcrossProfilesAndAllowsUnusedRemoval()
        {
            var workspace = Workspace();
            var bound = DcsSharedUiLayerCommandEditor.Commands(workspace)[0];
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Remove(workspace, bound));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(workspace, bound, bound with { Command = "OTHER" }));
            workspace = DcsSharedUiLayerBindingEditor.ClearLayerAssignments(workspace, Profile, "");
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Remove(workspace, bound));
            workspace = DcsSharedUiLayerBindingEditor.ClearLayerAssignments(workspace, Profile.Replace("Stick", "Axis", StringComparison.Ordinal), "SHIFT");
            workspace = DcsSharedUiLayerCommandEditor.Remove(workspace, bound);
            Assert.Contains(DcsSharedUiLayerBindingEditor.Bindings(workspace, Profile), x => x.CommandId == "BOUND" && x.Disposition == BindingDisposition.Removed);
            Assert.Empty(DcsSharedUiLayerCommandEditor.Commands(workspace));
            DcsSharedUiLayerBindingEditor.Validate(workspace);
        }
        /// <summary>Duplicate codes/identities, changed identities, invalid fields, and stale selections fail without mutation.</summary>
        [Fact]
        public void RejectsInvalidOrStaleEdits()
        {
            var workspace = Workspace();
            var current = DcsSharedUiLayerCommandEditor.Commands(workspace)[0];
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(workspace, null, current));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(workspace, null, current with { Id = "other" }));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(workspace, null, current with { Command = "OTHER" }));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(workspace, current, current with { Id = "other" }));
            foreach (var invalid in new[] { current with { Id = "" }, current with { Command = "A B" },
                current with { Label = " " }, current with { Category = "VR\n" }, current with { Id = " two " } })
                Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(workspace, current, invalid));
            var changed = DcsSharedUiLayerCommandEditor.Set(workspace, current, current with { Label = "Changed" });
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Set(changed, current, current));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Remove(changed, current));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerCommandEditor.Remove(workspace, current with { Id = "missing" }));
            Assert.Equal("Bound name", DcsSharedUiLayerCommandEditor.Commands(workspace)[0].Label);
        }
        /// <summary>Malformed and duplicate catalogs are rejected before a revision can be saved through the editor.</summary>
        /// <param name="source">Invalid catalog source.</param>
        [Theory]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData("{\"functions\":[null]}")]
        [InlineData("{\"functions\":[{}]}")]
        [InlineData("{\"functions\":[{\"id\":\"a\",\"command\":\"C\",\"label\":\"A\",\"category\":\"General\"},{\"id\":\"a\",\"command\":\"D\",\"label\":\"B\",\"category\":\"General\"}]}")]
        [InlineData("{\"functions\":[{\"id\":\"a\",\"command\":\"C\",\"label\":\"A\",\"category\":\"General\"},{\"id\":\"b\",\"command\":\"C\",\"label\":\"B\",\"category\":\"General\"}]}")]
        public void RejectsMalformedCatalogs(string source)
        {
            var workspace = Workspace();
            workspace = workspace with { Sources = new Dictionary<string, string>(workspace.Sources) { [Catalog] = source } };
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerBindingEditor.Validate(workspace));
        }
        /// <summary>Catalog drafts save and reopen as independent revisions while old revisions and module pins stay intact.</summary>
        /// <returns>Completion of the SQLite round trip.</returns>
        [Fact]
        public async Task SavesCatalogRevisionWithoutChangingModulePins()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-command-editor-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var modules = new DcsSqliteEditorWorkspaceRepository(connection);
                var module = new DcsEditorWorkspaceSnapshot(new DcsModuleConfiguration(new DcsModule(Guid.NewGuid(), "TEST", "Test"), [], [], [], "pinned"),
                    [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { [Catalog] = "module pinned catalog" });
                await modules.SaveAsync(module);
                var repository = new DcsSqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await repository.SaveAsync(Workspace() with { Revision = 0 });
                var draft = DcsSharedUiLayerCommandEditor.Set(first, null, new("new", "NEW", "New action", "General"));
                DcsSharedUiLayerBindingEditor.Validate(draft);
                var saved = await repository.SaveAsync(draft);
                var reopened = (await repository.GetAsync())!;
                Assert.Equal(2, reopened.Revision);
                Assert.Equal(saved.ContentSha256, reopened.ContentSha256);
                Assert.Contains(DcsSharedUiLayerCommandEditor.Commands(reopened), x => x.Command == "NEW");
                Assert.Equal(first.Sources[Catalog], (await repository.GetAsync(1))!.Sources[Catalog]);
                Assert.Equal(first.Sources[Profile], reopened.Sources[Profile]);
                Assert.Equal("module pinned catalog", (await modules.GetAsync(module.Configuration.Module.Id))!.Sources![Catalog]);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(draft));
            }
            finally { File.Delete(path); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsSharedUiLayerWorkspace Workspace() => new(1, new string('a', 40), new Dictionary<string, string> {
            [Catalog] = """{"schemaVersion":1,"extension":"root metadata","functions":[{"id":"bound","command":"BOUND","label":"Bound name","category":"General","extension":"entry metadata"}]}""",
            ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = """local modifiers = { ["SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN2", ["switch"] = false } } return modifiers""",
            [Profile] = """local diff = { ["keyDiffs"] = { ["BOUND"] = { ["name"] = "Imported old name", ["added"] = { [1] = { ["key"] = "JOY_BTN1" } }, ["removed"] = { [1] = { ["key"] = "JOY_BTN8" } } }, ["IMPORTED"] = { ["name"] = "Imported", ["added"] = { [1] = { ["key"] = "JOY_BTN99" } } } } } return diff""",
            [Profile.Replace("Stick", "Axis", StringComparison.Ordinal)] = """local diff = { ["axisDiffs"] = { ["BOUND"] = { ["name"] = "Axis", ["changed"] = { [1] = { ["key"] = "JOY_X", ["reformers"] = { [1] = "SHIFT" } } } } } } return diff"""
        }, "old hash");
        #endregion END private helpers
    }
}
