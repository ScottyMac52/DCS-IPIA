using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks shared modifier drafts, physical identities, binding safeguards, and isolated revision persistence.</summary>
    public sealed class SharedUiLayerModifierEditorTests
    {
        private const string Root = "assets/shared/ui-layer/input/UiLayer/";
        private const string Device = "Viper TQS {C0A33440-3F54-11f1-8001-444553540000}";
        private const string Profile = Root + "joystick/" + Device + ".diff.lua";
        #region BEGIN Public
        /// <summary>Unused definitions can be added, updated, renamed and removed without rewriting any profile or catalog.</summary>
        [Fact]
        public void StagesOnlyModifierSourceAndPreservesNativeNames()
        {
            var original = Workspace();
            Assert.Contains(Device, SharedUiLayerModifierEditor.Devices(original));
            Assert.Contains("Keyboard", SharedUiLayerModifierEditor.Devices(original));
            var added = new DcsModifier("ARM_SELECT_1", Device, "JOY_BTN55", ModifierMode.Toggle);
            var draft = SharedUiLayerModifierEditor.Set(original, null, added);
            Assert.Contains(added, SharedUiLayerModifierEditor.Modifiers(draft));
            Assert.Equal(original.Sources.Where(x => x.Key != Root + "modifiers.lua"), draft.Sources.Where(x => x.Key != Root + "modifiers.lua"));
            Assert.Equal(original.Revision, draft.Revision);
            Assert.Equal(original.SourceCommit, draft.SourceCommit);
            Assert.Null(draft.ContentSha256);
            Assert.DoesNotContain(added, SharedUiLayerModifierEditor.Modifiers(original));
            var changed = new DcsModifier("ARM_SELECT_2", Device, "JOY_BTN25", ModifierMode.Hold);
            draft = SharedUiLayerModifierEditor.Set(draft, added, changed);
            Assert.Contains(changed, SharedUiLayerModifierEditor.Modifiers(draft));
            draft = SharedUiLayerModifierEditor.Remove(draft, changed);
            Assert.Equal(SharedUiLayerModifierEditor.Modifiers(original), SharedUiLayerModifierEditor.Modifiers(draft));
            var escaped = new DcsModifier("Quoted \"name\" \\", Device, "JOY_BTN55", ModifierMode.Toggle);
            draft = SharedUiLayerModifierEditor.Set(draft, null, escaped);
            Assert.Contains(escaped, SharedUiLayerModifierEditor.Modifiers(draft));
            SharedUiLayerBindingEditor.Validate(draft);
        }
        /// <summary>Keys and axes in other profiles protect names; clearing those assignments leaves filters and removal history intact.</summary>
        [Fact]
        public void ProtectsBoundNamesAcrossAllProfiles()
        {
            var original = Workspace();
            var current = SharedUiLayerModifierEditor.Modifiers(original).Single(x => x.Name == "SHIFT");
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Remove(original, current));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Set(original, current,
                new("OTHER", Device, "JOY_BTN8", ModifierMode.Toggle)));
            var changed = new DcsModifier(current.Name, Device, "JOY_BTN8", ModifierMode.Toggle);
            var updated = SharedUiLayerModifierEditor.Set(original, current, changed);
            Assert.Contains(changed, SharedUiLayerModifierEditor.Modifiers(updated));
            Assert.Equal(original.Sources[Profile], updated.Sources[Profile]);
            updated = SharedUiLayerBindingEditor.ClearLayerAssignments(updated, Profile, "SHIFT");
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Remove(updated, changed));
            updated = SharedUiLayerBindingEditor.ClearLayerAssignments(updated, Root + "joystick/Axis.diff.lua", "SHIFT");
            updated = SharedUiLayerModifierEditor.Remove(updated, changed);
            Assert.Contains(SharedUiLayerBindingEditor.Bindings(updated, Profile), x => x.Disposition == BindingDisposition.Removed && x.Reformers.Contains("SHIFT"));
            Assert.Contains(SharedUiLayerBindingEditor.Bindings(original, Root + "joystick/Axis.diff.lua"), x => x.Filter?.Deadzone == 0.15);
            SharedUiLayerBindingEditor.Validate(updated);
        }
        /// <summary>Keyboard defaults, invalid fields, duplicate names, physical aliases, and stale rows cannot be changed silently.</summary>
        [Fact]
        public void RejectsInvalidDuplicateProtectedAndStaleDefinitions()
        {
            var original = Workspace();
            var shift = SharedUiLayerModifierEditor.Modifiers(original).Single(x => x.Name == "SHIFT");
            var keyboard = SharedUiLayerModifierEditor.Modifiers(original).Single(x => x.Name == "LCtrl");
            Assert.True(SharedUiLayerModifierEditor.IsProtected(keyboard));
            Assert.False(SharedUiLayerModifierEditor.IsProtected(shift));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Remove(original, keyboard));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Set(original, keyboard, new("CUSTOM", Device, "JOY_BTN99", ModifierMode.Hold)));
            foreach (var candidate in new[] {
                new DcsModifier("shift", Device, "JOY_BTN99", ModifierMode.Hold),
                new DcsModifier("OTHER", Device, "JOY_BTN7", ModifierMode.Toggle),
                new DcsModifier("BAD+NAME", Device, "JOY_BTN99", ModifierMode.Hold),
                new DcsModifier("BAD\nNAME", Device, "JOY_BTN99", ModifierMode.Hold),
                new DcsModifier("OTHER", Device, "JOY BTN99", ModifierMode.Hold),
                new DcsModifier("OTHER", "Unknown device", "JOY_BTN99", ModifierMode.Hold),
                new DcsModifier("OTHER", Device, "JOY_BTN99", (ModifierMode)99),
                new DcsModifier("LAlt", Device, "JOY_BTN99", ModifierMode.Hold)
            }) Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Set(original, null, candidate));
            var changed = SharedUiLayerModifierEditor.Set(original, shift, new("SHIFT", Device, "JOY_BTN8", ModifierMode.Hold));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Remove(changed, shift));
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Set(changed, shift, shift));
            var pov = new DcsModifier("POV", Device, "JOY_POV1_D", ModifierMode.Hold);
            var draft = SharedUiLayerModifierEditor.Set(original, null, pov);
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Set(draft, null, new("ALIAS", Device, "JOY_BTN_POV1_D", ModifierMode.Toggle)));
            var sources = draft.Sources.ToDictionary(x => x.Key, x => x.Value);
            sources[Root + "modifiers.lua"] = sources[Root + "modifiers.lua"].Replace("return modifiers", "", StringComparison.Ordinal)
                .Replace("}\n", "  [\"ALIAS\"] = { [\"device\"] = \"" + Device + "\", [\"key\"] = \"JOY_BTN_POV1_D\", [\"switch\"] = true },\n}\n", StringComparison.Ordinal) + "return modifiers\n";
            Assert.Throws<InvalidOperationException>(() => SharedUiLayerModifierEditor.Validate(draft with { Sources = sources }));
        }
        /// <summary>New revisions reopen with exact modifier identities and do not mutate module overrides or history.</summary>
        [Fact]
        public async Task PersistsIndependentModifierRevision()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-shared-modifiers-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var modules = new SqliteEditorWorkspaceRepository(connection);
                var module = new EditorWorkspaceSnapshot(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "TEST", "Test"), [], [], [], "test"),
                    [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { ["override"] = "unchanged" });
                await modules.SaveAsync(module);
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await repository.SaveAsync(Workspace() with { Revision = 0 });
                var added = new DcsModifier("ARM_SELECT_1", Device, "JOY_BTN55", ModifierMode.Toggle);
                var draft = SharedUiLayerModifierEditor.Set(first, null, added);
                SharedUiLayerBindingEditor.Validate(draft);
                var second = await repository.SaveAsync(draft);
                Assert.Equal(2, second.Revision);
                var reopened = (await new SqliteSharedUiLayerWorkspaceRepository(connection).GetAsync())!;
                Assert.Contains(added, SharedUiLayerModifierEditor.Modifiers(reopened));
                Assert.DoesNotContain(added, SharedUiLayerModifierEditor.Modifiers((await repository.GetAsync(1))!));
                Assert.Equal(first.ContentSha256, (await repository.GetAsync(1))!.ContentSha256);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(draft));
                Assert.Equal("unchanged", (await modules.GetAsync(module.Configuration.Module.Id))!.Sources!["override"]);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedUiLayerWorkspace Workspace() => new(3, new string('a', 40), new Dictionary<string, string> {
            [Root + "modifiers.lua"] = "local modifiers = { [\"SHIFT\"] = { [\"device\"] = \"" + Device + "\", [\"key\"] = \"JOY_BTN7\", [\"switch\"] = false }, [\"LCtrl\"] = { [\"device\"] = \"Keyboard\", [\"key\"] = \"LCtrl\", [\"switch\"] = false } } return modifiers",
            [Profile] = """
                local diff = { ["keyDiffs"] = { ["A"] = {
                    ["added"] = { [1] = { ["key"] = "JOY_BTN1", ["reformers"] = { [1] = "SHIFT" } } },
                    ["removed"] = { [1] = { ["key"] = "JOY_BTN2", ["reformers"] = { [1] = "SHIFT" } } }, ["name"] = "Action" } }, ["axisDiffs"] = {} } return diff
                """,
            [Root + "joystick/Axis.diff.lua"] = """
                local diff = { ["keyDiffs"] = {}, ["axisDiffs"] = { ["AXIS"] = { ["name"] = "Axis",
                  ["added"] = { [1] = { ["key"] = "JOY_X", ["reformers"] = { [1] = "SHIFT" }, ["filter"] = { ["deadzone"] = 0.15 } } } } } } return diff
                """,
            ["assets/shared/ui-layer/functions.json"] = """{"functions":[]}""",
            ["assets/shared/ui-layer/extra.txt"] = "exact untouched source"
        }, "baseline hash");
        #endregion END private helpers
    }
}
