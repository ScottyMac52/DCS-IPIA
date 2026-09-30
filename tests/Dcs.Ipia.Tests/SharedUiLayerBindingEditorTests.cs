using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks safe shared binding drafts, source preservation, revision concurrency, and module isolation.</summary>
    public sealed class SharedUiLayerBindingEditorTests
    {
        private const string PathKey = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua";
        #region BEGIN Public
        /// <summary>Updates, adds, and removes keys while retaining untouched files, axis filters, and removed history.</summary>
        [Fact]
        public void PreservesUnrelatedSourcesAndBindings()
        {
            var baseline = Workspace();
            var old = SharedUiLayerBindingEditor.Bindings(baseline, PathKey).Single(x => x.CommandId == "OLD");
            var edited = SharedUiLayerBindingEditor.Set(baseline, PathKey, old, "NEW", "JOY_BTN3", ["SHIFT"]);
            Assert.Equal(baseline.Revision, edited.Revision);
            Assert.Null(edited.ContentSha256);
            Assert.Equal(baseline.Sources.Where(x => x.Key != PathKey), edited.Sources.Where(x => x.Key != PathKey));
            Assert.Contains("OLD", baseline.Sources[PathKey]);
            Assert.Contains(SharedUiLayerBindingEditor.Bindings(edited, PathKey), x => x.CommandId == "NEW" && x.InputKey == "JOY_BTN3" && x.Reformers.SequenceEqual(["SHIFT"]));
            var protectedRows = SharedUiLayerBindingEditor.Bindings(baseline, PathKey).Where(x => x.CommandId != "OLD");
            foreach (var row in protectedRows)
                Assert.Contains(SharedUiLayerBindingEditor.Bindings(edited, PathKey), x => JsonSerializer.Serialize(x) == JsonSerializer.Serialize(row));
            edited = SharedUiLayerBindingEditor.Set(edited, PathKey, null, "NEW", "JOY_BTN4", []);
            SharedUiLayerBindingEditor.Validate(edited);
            var added = SharedUiLayerBindingEditor.Bindings(edited, PathKey).Single(x => x.InputKey == "JOY_BTN4");
            edited = SharedUiLayerBindingEditor.Remove(edited, PathKey, added);
            Assert.DoesNotContain(SharedUiLayerBindingEditor.Bindings(edited, PathKey), x => x.InputKey == "JOY_BTN4");
            Assert.Contains(SharedUiLayerBindingEditor.Commands(edited), x => x.Id == "UNMAPPED");
        }
        /// <summary>Unsafe conflicts, unknown modifiers/commands, invalid selections, and protected rows fail without mutating defaults.</summary>
        [Fact]
        public void RejectsInvalidEditsAndProtectsHistory()
        {
            var workspace = Workspace();
            var rows = SharedUiLayerBindingEditor.Bindings(workspace, PathKey);
            var current = rows.Single(x => x.CommandId == "OLD");
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Bindings(workspace, "unknown"));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Set(workspace, PathKey, null, "UNKNOWN", "JOY_BTN3", []));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Set(workspace, PathKey, null, "NEW", "\n", []));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Set(workspace, PathKey, null, "NEW", "JOY_BTN3", ["UNKNOWN"]));
            Assert.Throws<ArgumentException>(() => SharedUiLayerBindingEditor.Set(workspace, PathKey, null, "NEW", "JOY_BTN3", ["SHIFT", "SHIFT"]));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Set(workspace, PathKey, null, "NEW", "JOY_BTN1", []));
            foreach (var row in rows.Where(x => x.Section == BindingSection.Axis || x.Disposition == BindingDisposition.Removed))
            {
                Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Set(workspace, PathKey, row, "NEW", "JOY_BTN3", []));
                Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Remove(workspace, PathKey, row));
            }
            var changed = SharedUiLayerBindingEditor.Set(workspace, PathKey, current, "NEW", "JOY_BTN3", []);
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Remove(changed, PathKey, current));
            var sources = new Dictionary<string, string>(workspace.Sources) { [PathKey] = workspace.Sources[PathKey].Replace("JOY_BTN99", "JOY_BTN1", StringComparison.Ordinal) };
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerBindingEditor.Validate(workspace with { Sources = sources }));
        }
        /// <summary>Edited defaults reopen as a new revision, retaining history and leaving module sources intact.</summary>
        /// <returns>Completion of the real SQLite round trip.</returns>
        [Fact]
        public async Task SavesIndependentRevisionWithoutChangingModules()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-default-edit-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var modules = new SqliteEditorWorkspaceRepository(connection);
                var module = new EditorWorkspaceSnapshot(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "TEST", "Test"), [], [], [], "pinned"),
                    [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { [PathKey] = "module override" });
                await modules.SaveAsync(module);
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await repository.SaveAsync(Workspace() with { Revision = 0 });
                var current = SharedUiLayerBindingEditor.Bindings(first, PathKey).Single(x => x.CommandId == "OLD");
                var draft = SharedUiLayerBindingEditor.Set(first, PathKey, current, "NEW", "JOY_BTN3", ["SHIFT"]);
                SharedUiLayerBindingEditor.Validate(draft);
                var saved = await repository.SaveAsync(draft);
                var reopened = (await repository.GetAsync())!;
                Assert.Equal(2, reopened.Revision);
                Assert.Equal(saved.ContentSha256, reopened.ContentSha256);
                Assert.Contains(SharedUiLayerBindingEditor.Bindings(reopened, PathKey), x => x.CommandId == "NEW");
                Assert.Equal(first.Sources[PathKey], (await repository.GetAsync(1))!.Sources[PathKey]);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(draft));
                Assert.Equal("module override", (await modules.GetAsync(module.Configuration.Module.Id))!.Sources![PathKey]);
            }
            finally { File.Delete(file); }
        }
        /// <summary>The shipped default revision is valid for binding editing without changing its files.</summary>
        /// <returns>Completion of bundled asset validation.</returns>
        [Fact]
        public async Task AcceptsBundledDefaultBindings()
        {
            var workspace = await SharedUiLayerWorkspaceFactory.CreateAsync();
            SharedUiLayerBindingEditor.Validate(workspace);
            Assert.NotEmpty(SharedUiLayerBindingEditor.Commands(workspace));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedUiLayerWorkspace Workspace() => new(1, new string('a', 40), new Dictionary<string, string> {
            ["assets/shared/ui-layer/functions.json"] = """{"functions":[{"command":"NEW","label":"New command"}]}""",
            ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = """local modifiers = { ["SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN2", ["switch"] = false } } return modifiers""",
            [PathKey] = """
                local diff = {
                 ["keyDiffs"] = {
                  ["OLD"] = { ["name"] = "Old command", ["added"] = { [1] = { ["key"] = "JOY_BTN1" } } },
                  ["UNMAPPED"] = { ["name"] = "Unmapped", ["added"] = { [1] = { ["key"] = "JOY_BTN99" } } },
                  ["REMOVED"] = { ["name"] = "History", ["removed"] = { [1] = { ["key"] = "JOY_BTN8" } } }
                 },
                 ["axisDiffs"] = { ["AXIS"] = { ["name"] = "Axis", ["changed"] = { [1] = { ["key"] = "JOY_X", ["filter"] = { ["deadzone"] = 0.1, ["invert"] = true, ["curvature"] = { [1] = 0.2 } } } } } }
                } return diff
                """
        }, "old hash");
        #endregion END private helpers
    }
}
