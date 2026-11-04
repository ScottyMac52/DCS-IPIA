using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks independent shared profile edits, physical identities, preservation, and SQLite revision isolation.</summary>
    public sealed class SharedUiLayerProfileEditorTests
    {
        private const string Root = "assets/shared/ui-layer/input/UiLayer/";
        private const string Original = "Viper TQS {C0A33440-3F54-11f1-8001-444553540000}.diff.lua";
        private const string Renamed = "Renamed TQS {C0A33440-3F54-11f1-8001-444553540000}.diff.lua";
        private const string Second = "Second TQS {C0A33441-3F54-11f1-8001-444553540000}.diff.lua";
        private const string Third = "Third TQS {C0A33442-3F54-11f1-8001-444553540000}.diff.lua";
        #region BEGIN Public
        /// <summary>Empty and copied devices preserve exact Lua, filters, history, provenance, and untouched sources.</summary>
        [Fact]
        public void AddsEmptyAndClonedJoystickProfiles()
        {
            var baseline = Workspace();
            var source = Profile(baseline, Original);
            Assert.Equal(2, source.ActiveBindings);
            Assert.Equal(1, source.ModifierCount);
            var draft = DcsSharedUiLayerProfileEditor.Add(baseline, Second);
            Assert.Empty(DcsSharedUiLayerBindingEditor.Bindings(draft, Root + "joystick/" + Second));
            draft = DcsSharedUiLayerProfileEditor.Add(draft, Third, source);
            Assert.Equal(source.Source, draft.Sources[Root + "joystick/" + Third]);
            Assert.Equal(baseline.Sources, draft.Sources.Where(x => baseline.Sources.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value));
            Assert.Equal(baseline.Revision, draft.Revision);
            Assert.Equal(baseline.SourceCommit, draft.SourceCommit);
            Assert.Null(draft.ContentSha256);
            Assert.Contains(Third[..^9], DcsSharedUiLayerModifierEditor.Devices(draft));
            Assert.Contains(DcsSharedUiLayerBindingEditor.Bindings(draft, Root + "joystick/" + Third), x => x.Filter?.Deadzone == 0.15);
            Assert.Contains(DcsSharedUiLayerBindingEditor.Bindings(draft, Root + "joystick/" + Third), x => x.Disposition == BindingDisposition.Removed);
            DcsSharedUiLayerBindingEditor.Validate(draft);
            Assert.DoesNotContain(Root + "joystick/" + Second, baseline.Sources.Keys);
        }
        /// <summary>Renaming updates bound modifier activators and retains every binding byte and physical GUID.</summary>
        [Fact]
        public void RenamesNativeProfileAndModifierDeviceTogether()
        {
            var baseline = Workspace();
            var current = Profile(baseline, Original);
            var draft = DcsSharedUiLayerProfileEditor.Rename(baseline, current, Renamed);
            Assert.DoesNotContain(current.Path, draft.Sources.Keys);
            Assert.Equal(current.Source, draft.Sources[Root + "joystick/" + Renamed]);
            var modifier = DcsSharedUiLayerModifierEditor.Modifiers(draft).Single(x => x.Name == "SHIFT");
            Assert.Equal(Renamed[..^9], modifier.Device);
            Assert.Equal("JOY_BTN7", modifier.Key);
            Assert.Equal(ModifierMode.Toggle, modifier.Mode);
            Assert.Equal(baseline.Sources[Root + "keyboard/Keyboard.diff.lua"], draft.Sources[Root + "keyboard/Keyboard.diff.lua"]);
            Assert.Equal(baseline.Sources["assets/shared/ui-layer/hardware-overlays.json"], draft.Sources["assets/shared/ui-layer/hardware-overlays.json"]);
            DcsSharedUiLayerBindingEditor.Validate(draft);
            Assert.Contains(current.Path, baseline.Sources.Keys);
            var exact = DcsSharedUiLayerProfileEditor.Rename(draft, Profile(draft, Renamed), Renamed.Replace("11f1", "11F1", StringComparison.Ordinal));
            Assert.Contains(DcsSharedUiLayerModifierEditor.Modifiers(exact), x => x.Device.Contains("11F1", StringComparison.Ordinal));
        }
        /// <summary>Deletion requires both keys/axes and modifier references to be cleared, while keyboard profiles stay protected.</summary>
        [Fact]
        public void ProtectsBindingsModifiersKeyboardAndStaleSelections()
        {
            var baseline = Workspace();
            var current = Profile(baseline, Original);
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Remove(baseline, current));
            var draft = DcsSharedUiLayerBindingEditor.ClearLayerAssignments(baseline, current.Path, "");
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Remove(draft, Profile(draft, Original)));
            draft = DcsSharedUiLayerBindingEditor.ClearLayerAssignments(draft, current.Path, "SHIFT");
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Remove(draft, Profile(draft, Original)));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Remove(draft, current));
            var modifier = DcsSharedUiLayerModifierEditor.Modifiers(draft).Single(x => x.Name == "SHIFT");
            draft = DcsSharedUiLayerModifierEditor.Remove(draft, modifier);
            draft = DcsSharedUiLayerProfileEditor.Remove(draft, Profile(draft, Original));
            Assert.DoesNotContain(current.Path, draft.Sources.Keys);
            DcsSharedUiLayerBindingEditor.Validate(draft);
            var keyboard = Profile(baseline, "Keyboard.diff.lua");
            Assert.True(keyboard.Protected);
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Remove(baseline, keyboard));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Rename(baseline, keyboard, Second));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Add(baseline, Second, keyboard));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Rename(draft, current, Renamed));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Add(draft, Second, current));
        }
        /// <summary>New names must be safe native files, cannot duplicate a physical unit, and cannot reassign a profile's GUID.</summary>
        [Fact]
        public void RejectsInvalidNamesAndDuplicateIdentities()
        {
            var baseline = Workspace();
            foreach (var file in new[] { "", "../" + Second, " " + Second, "Device.diff.lua", "Device {invalid}.diff.lua",
                "Device {00000000-0000-0000-0000-000000000000}.diff.lua", Second.Replace("Second", "Bad\nName", StringComparison.Ordinal),
                Original, Renamed })
                Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Add(baseline, file));
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Rename(baseline, Profile(baseline, Original), Second));
            var draft = DcsSharedUiLayerProfileEditor.Add(baseline, Second);
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Add(draft, Second.ToUpperInvariant()));
            var sources = new Dictionary<string, string>(baseline.Sources) { [Root + "joystick/" + Renamed] = Profile(baseline, Original).Source };
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Validate(baseline with { Sources = sources }));
            sources.Remove(Root + "joystick/" + Renamed);
            sources[Root + "joystick/sub/" + Original] = Profile(baseline, Original).Source;
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Validate(baseline with { Sources = sources }));
            sources.Remove(Root + "joystick/sub/" + Original);
            sources[Root + "joystick/" + Original.Replace("Viper", "viper", StringComparison.Ordinal)] = Profile(baseline, Original).Source;
            Assert.Throws<InvalidOperationException>(() => DcsSharedUiLayerProfileEditor.Validate(baseline with { Sources = sources }));
        }
        /// <summary>Saved profile additions, renames and removals retain old revisions and isolated module overrides.</summary>
        [Fact]
        public async Task PersistsProfileRevisionHistoryWithoutChangingModules()
        {
            var file = Path.Combine(Path.GetTempPath(), "ipia-profile-editor-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var modules = new DcsSqliteEditorWorkspaceRepository(connection);
                var module = new DcsEditorWorkspaceSnapshot(new DcsModuleConfiguration(new DcsModule(Guid.NewGuid(), "TEST", "Test"), [], [], [], "test"),
                    [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { ["override"] = "unchanged" });
                await modules.SaveAsync(module);
                var repository = new DcsSqliteSharedUiLayerWorkspaceRepository(connection);
                var first = await repository.SaveAsync(Workspace());
                var draft = DcsSharedUiLayerProfileEditor.Rename(first, Profile(first, Original), Renamed);
                draft = DcsSharedUiLayerProfileEditor.Add(draft, Second);
                DcsSharedUiLayerBindingEditor.Validate(draft);
                var second = await repository.SaveAsync(draft);
                var reopened = (await new DcsSqliteSharedUiLayerWorkspaceRepository(connection).GetAsync())!;
                Assert.Equal(2, reopened.Revision);
                Assert.Equal(Profile(first, Original).Source, Profile(reopened, Renamed).Source);
                Assert.Equal(0, Profile(reopened, Second).ActiveBindings);
                Assert.Equal(Renamed[..^9], DcsSharedUiLayerModifierEditor.Modifiers(reopened).Single(x => x.Name == "SHIFT").Device);
                var third = await repository.SaveAsync(DcsSharedUiLayerProfileEditor.Remove(reopened, Profile(reopened, Second)));
                Assert.Equal(3, third.Revision);
                Assert.DoesNotContain(Root + "joystick/" + Second, third.Sources.Keys);
                Assert.Contains(Root + "joystick/" + Second, (await repository.GetAsync(2))!.Sources.Keys);
                Assert.Equal(first.ContentSha256, (await repository.GetAsync(1))!.ContentSha256);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(second));
                Assert.Equal("unchanged", (await modules.GetAsync(module.Configuration.Module.Id))!.Sources!["override"]);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsSharedUiLayerDeviceProfile Profile(DcsSharedUiLayerWorkspace workspace, string file) =>
            DcsSharedUiLayerProfileEditor.Profiles(workspace).Single(x => x.FileName == file);
        private static DcsSharedUiLayerWorkspace Workspace() => new(0, new string('a', 40), new Dictionary<string, string> {
            [Root + "modifiers.lua"] = "local modifiers = { [\"SHIFT\"] = { [\"device\"] = \"" + Original[..^9] + "\", [\"key\"] = \"JOY_BTN7\", [\"switch\"] = true }, [\"LCtrl\"] = { [\"device\"] = \"Keyboard\", [\"key\"] = \"LCtrl\", [\"switch\"] = false } } return modifiers",
            [Root + "joystick/" + Original] = """
                -- exact profile comment
                local diff = { ["keyDiffs"] = { ["A"] = { ["name"] = "Action",
                  ["added"] = { [1] = { ["key"] = "JOY_BTN1" } }, ["removed"] = { [1] = { ["key"] = "JOY_BTN2" } } } },
                  ["axisDiffs"] = { ["AXIS"] = { ["name"] = "Axis", ["added"] = { [1] = { ["key"] = "JOY_X",
                    ["reformers"] = { [1] = "SHIFT" }, ["filter"] = { ["deadzone"] = 0.15 } } } } } } return diff
                """,
            [Root + "keyboard/Keyboard.diff.lua"] = "local diff = { [\"keyDiffs\"] = {}, [\"axisDiffs\"] = {} } return diff",
            ["assets/shared/ui-layer/functions.json"] = """{"functions":[]}""",
            ["assets/shared/ui-layer/hardware-overlays.json"] = """{"extension":"exact untouched metadata"}"""
        });
        #endregion END private helpers
    }
}
