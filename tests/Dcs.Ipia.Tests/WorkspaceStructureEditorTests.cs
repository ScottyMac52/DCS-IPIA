using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises module-owned device and modifier management through generated Lua and saved settings.</summary>
    public sealed class WorkspaceStructureEditorTests
    {
        private static readonly Guid ModuleId = Guid.Parse("115c5cf8-c3ac-4d80-a512-5d92bd36856a");
        private static readonly Guid ProfileId = Guid.Parse("b2da76ec-5d9a-497d-9712-43864c304939");
        #region BEGIN Public
        /// <summary>New devices use editable filenames, optional identity fields, and independent scope membership.</summary>
        [Fact]
        public void AddsUpdatesAndExportsScopeSpecificDevices()
        {
            var saved = Workspace();
            var hardware = new WorkspaceHardware("other", "Other device", "Default.diff.lua", saved.Diagrams[0]);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.UiLayer, "Custom UI.diff.lua", null, null);
            var state = WorkspaceStructureEditor.Describe(saved);
            var added = state.Devices.Single(x => x.FileName == "Custom UI.diff.lua");
            Assert.Null(added.Instance);
            Assert.Null(added.Role);
            Assert.DoesNotContain(state.Devices, x => x.ProfileId == added.ProfileId && x.Layer == AssignmentLayer.Module);
            var payload = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("Config/Input/UiLayer/joystick/Custom UI.diff.lua", payload.Keys);
            Assert.DoesNotContain("Config/Input/TEST/joystick/Custom UI.diff.lua", payload.Keys);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, added, "Renamed UI.diff.lua", "SECOND", "Auxiliary");
            Assert.Contains("Config/Input/UiLayer/joystick/Renamed UI.diff.lua", SnapshotLuaPayloadRenderer.Generate(saved).Keys);
            var json = saved.Sources![WorkspaceManagement.SourcePath];
            var restored = JsonSerializer.Deserialize<WorkspaceManagement>(json)!;
            Assert.Equal("Auxiliary", restored.Devices.Single(x => x.ProfileId == added.ProfileId).Role);
            saved = WorkspaceStructureEditor.RemoveDevice(saved, WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.ProfileId == added.ProfileId));
            Assert.DoesNotContain(saved.Configuration.Profiles, x => x.Id == added.ProfileId);
            Assert.DoesNotContain(saved.Diagrams, x => x.ProfileId == added.ProfileId);
            Assert.DoesNotContain(saved.Pages, x => x.StableKey == "device-" + added.ProfileId.ToString("N"));
        }
        /// <summary>Removal checks UI use and unmapped bindings, and explicit clearing makes removal possible.</summary>
        [Fact]
        public void BlocksUsedDevicesUntilBothScopesAndActivatorsAreCleared()
        {
            var saved = Workspace();
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveDevice(saved, device));
            var onlyUi = saved with { Assignments = saved.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer).ToArray() };
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveDevice(onlyUi, device));
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, device);
            Assert.Empty(saved.Assignments);
            Assert.DoesNotContain(DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(saved, ProfileId), ProfileId),
                x => x.Disposition != BindingDisposition.Removed);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveDevice(saved, device));
            foreach (var modifier in WorkspaceStructureEditor.Describe(saved).Modifiers.ToArray())
                saved = WorkspaceStructureEditor.RemoveModifier(saved, modifier);
            foreach (var membership in WorkspaceStructureEditor.Describe(saved).Devices.ToArray())
                saved = WorkspaceStructureEditor.RemoveDevice(saved, membership);
            Assert.Empty(saved.Configuration.Profiles);
        }
        /// <summary>Modifier updates preserve used references and labels, while removal checks native and semantic uses across scopes.</summary>
        [Fact]
        public void RenamesUsedModifiersAndChecksCrossScopeUsage()
        {
            var saved = Workspace();
            var shift = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.SetModifier(saved, shift, shift with { Name = "RENAMED", SemanticLayer = "Combat", Mode = ModifierMode.Toggle });
            var renamed = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.Module);
            Assert.Contains(saved.Assignments, x => x.Slot.Layer == AssignmentLayer.Module && x.Slot.ModifierChord == "RENAMED");
            Assert.Contains(saved.Labels, x => x.Key == saved.Assignments.Single(a => a.Slot.Layer == AssignmentLayer.Module).Slot.Key && x.Value == "My label");
            var payload = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("RENAMED", Encoding.UTF8.GetString(payload["Config/Input/TEST/joystick/Stick.diff.lua"]));
            Assert.Contains("[\"switch\"] = true", Encoding.UTF8.GetString(payload["Config/Input/TEST/modifiers.lua"]));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveModifier(saved, renamed));
            var ui = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.SetModifier(saved, ui, ui with { SemanticLayer = "Combat" });
            var moduleCleared = saved with { Assignments = saved.Assignments.Where(x => x.Slot.Layer != AssignmentLayer.Module).ToArray() };
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveModifier(moduleCleared, renamed));
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, WorkspaceStructureEditor.Describe(saved).Devices[0]);
            saved = WorkspaceStructureEditor.RemoveModifier(saved, renamed);
            Assert.DoesNotContain(WorkspaceStructureEditor.Describe(saved).Modifiers, x => x.Name == "RENAMED");
        }
        /// <summary>UI modifier renames migrate mapped and unmapped bindings without losing either.</summary>
        [Fact]
        public void RenamesUiModifierAndKeepsUnmappedBindings()
        {
            var saved = Workspace();
            var ui = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.SetModifier(saved, ui, ui with { Name = "VOICE_SHIFT", SemanticLayer = "Voice", Mode = ModifierMode.Toggle });
            var bindings = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(saved, ProfileId), ProfileId);
            Assert.Contains(bindings, x => x.CommandId == "UNMAPPED");
            Assert.All(bindings, x => Assert.Contains("VOICE_SHIFT", x.Reformers));
            Assert.DoesNotContain(saved.Assignments, x => x.Slot.Layer == AssignmentLayer.UiLayer && x.Slot.ModifierChord == "UI_SHIFT");
        }
        /// <summary>Digital shared controls and valid unique identities are required before a change can be staged.</summary>
        [Fact]
        public void RejectsInvalidDevicesAndModifiers()
        {
            var saved = Workspace();
            var device = WorkspaceStructureEditor.Describe(saved).Devices[0];
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, "../bad.diff.lua", null, null));
            var hardware = new WorkspaceHardware("other", "Other", "Other.diff.lua", saved.Diagrams[0]);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "Stick.diff.lua", null, null));
            var modifier = new WorkspaceModifier(AssignmentLayer.Module, ProfileId, "NEW", "JOY_X", "Layer", ModifierMode.Hold, "");
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier with { Key = "JOY_BTN2", Name = "SHIFT" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier with { Key = "JOY_BTN1", SemanticLayer = "" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier with { Key = "JOY_BTN1", Name = "A+B" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, "Other.diff.lua", null, null, hardware: hardware));
        }
        /// <summary>New modifiers become valid assignment layers and exports use the edited activator filename.</summary>
        [Fact]
        public void AddsModifiersToBothScopesAndUpdatesNativeDeviceName()
        {
            var saved = Workspace();
            foreach (var layer in new[] { AssignmentLayer.Module, AssignmentLayer.UiLayer })
                saved = WorkspaceStructureEditor.SetModifier(saved, null,
                    new(layer, ProfileId, "EXTRA", "JOY_BTN1", "Extra Layer", ModifierMode.Hold, ""));
            Assert.Contains("EXTRA", WorkspaceStructureEditor.UiModifierNames(saved));\n            var commands = saved.Assignments.Select(x => x.Command).Distinct();
            var draft = new DraftAssignmentService(saved.Configuration, commands, saved.Assignments, WorkspaceStructureEditor.UiModifierNames(saved));
            var slot = new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1", "EXTRA");
            Assert.Equal(DraftOutcome.Applied, draft.Assign(slot, new("VOICE", "Voice", false, AssignmentLayer.UiLayer), OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
            saved = saved with { Assignments = draft.Snapshot };
            Assert.Contains("EXTRA", UiLayerLuaProfileRenderer.Render(saved, ProfileId));

            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, "Renamed stick.diff.lua", null, null);
            var lua = Encoding.UTF8.GetString(SnapshotLuaPayloadRenderer.Generate(saved)["Config/Input/TEST/modifiers.lua"]);
            Assert.All(DcsLuaParser.ParseModifiers(lua), x => Assert.Equal("Renamed stick", x.Device));
            Assert.Contains("Config/Input/TEST/joystick/Renamed stick.diff.lua", SnapshotLuaPayloadRenderer.Generate(saved).Keys);
        }
        /// <summary>Saved device membership, semantic modifiers, and the hardware catalog reopen without source checkouts.</summary>
        /// <returns>Completion of the SQLite round trip.</returns>
        [Fact]
        public async Task ReopensManagementAndHardwareFromSqlite()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-managed-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var saved = Workspace();
                var hardware = new WorkspaceHardware("other", "Other", "Other.diff.lua", saved.Diagrams[0]);
                saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "Custom.diff.lua", "AUX", null);
                var added = WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.FileName == "Custom.diff.lua");
                saved = WorkspaceStructureEditor.SetModifier(saved, null,
                    new(AssignmentLayer.Module, added.ProfileId, "AUX_SHIFT", "JOY_BTN2", "Auxiliary", ModifierMode.Toggle, ""));
                var sources = new Dictionary<string, string>(saved.Sources!) { ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { hardware }) };
                saved = saved with { Sources = sources };
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var restored = (await repository.GetAsync(ModuleId))!;
                Assert.Equal("Custom.diff.lua", WorkspaceStructureEditor.Describe(restored).Devices.Single(x => x.ProfileId == added.ProfileId).FileName);
                var modifier = WorkspaceStructureEditor.Describe(restored).Modifiers.Single(x => x.Name == "AUX_SHIFT");
                Assert.Equal("Auxiliary", modifier.SemanticLayer);
                Assert.Equal(ModifierMode.Toggle, modifier.Mode);
                Assert.Equal("Other.diff.lua", Assert.Single(WorkspaceStructureEditor.Hardware(restored)).DefaultFileName);
                Assert.Contains("Config/Input/TEST/joystick/Custom.diff.lua", SnapshotLuaPayloadRenderer.Generate(restored).Keys);
            }
            finally { File.Delete(path); }
        }
        /// <summary>Module MFD edits reach the combined kneeboard, including its middle callout.</summary>
        [Fact]
        public void UpdatesMfdCategoryLabels()
        {
            var saved = Workspace("tm-mfd");
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, null, null,
                new Dictionary<string, string> { ["left"] = "COMM1", ["right"] = "COMM2", ["top"] = "UFC", ["middle"] = "RADAR" });
            var page = KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels);
            Assert.Equal("COMM1", page.CategoryLabels["mfd-category-left"]);
            Assert.Equal("COMM2", page.CategoryLabels["mfd-category-right"]);
            Assert.Equal("UFC", page.CategoryLabels["mfd-category-top"]);
            Assert.Equal("RADAR", page.CategoryLabels["mfd-category-middle"]);
            Assert.Contains("meta-mfd-category-middle", saved.Diagrams[0].Svg);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot Workspace(string deviceId = "stick")
        {
            var profile = new DeviceProfile(ProfileId, deviceId, null, "Stick", "Stick.diff.lua");
            var config = new ModuleConfiguration(new DcsModule(ModuleId, "TEST", "Test"), [profile],
                [new DcsModifier("SHIFT", "Stick", "JOY_BTN2", ModifierMode.Hold)],
                [new DcsBinding(ProfileId, BindingSection.Key, "FLIGHT", "Flight", BindingDisposition.Added, "JOY_BTN1", ["SHIFT"])], "fixture");
            var assignments = new[] {
                new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", "SHIFT"), new("FLIGHT", "Flight", false, AssignmentLayer.Module)),
                new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1", "UI_SHIFT"), new("VOICE", "Voice", false, AssignmentLayer.UiLayer))
            };
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [],
                [new(1, "one", "Voice", "UI_SHIFT", 0, 0, 10, 10) { CommandId = "VOICE" }]) {
                ProfileId = ProfileId, CanonicalUiLayerSourcePath = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua",
                Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100"><text id="meta-mfd-category-top" x="50" y="20"/><text id="meta-mfd-category-bottom" x="50" y="80"/></svg>""",
                Controls = [new("one", "Button 1", "JOY_BTN1", 0, 0, 10, 10, false), new("two", "Button 2", "JOY_BTN2", 10, 0, 10, 10, false), new("axis", "Axis", "JOY_X", 20, 0, 10, 10, true)]
            };
            var sources = new Dictionary<string, string> {
                ["config/kneeboard.json"] = """{"includeUiLayer":true,"profiles":{"stick":"src/Config/Input/TEST/joystick/Stick.diff.lua"},"pages":[{"file":"01-STICK","profile":"stick","title":"Stick","kicker":"Controls","categoryLabels":{}}],"summaryPages":[]}""",
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = """local modifiers = { ["UI_SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN2", ["switch"] = false } } return modifiers""",
                ["assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua"] = """local diff = { ["keyDiffs"] = { ["VOICE"] = { ["name"] = "Voice", ["added"] = { [1] = { ["key"] = "JOY_BTN1", ["reformers"] = { [1] = "UI_SHIFT" } } } }, ["UNMAPPED"] = { ["name"] = "Unmapped", ["added"] = { [1] = { ["key"] = "JOY_BTN99", ["reformers"] = { [1] = "UI_SHIFT" } } } } } } return diff"""
            };
            return new(config, [new KneeboardPage(Guid.NewGuid(), ModuleId, null, KneeboardPageKind.Device, "01-STICK", "Stick", [], 0)],
                [], [diagram], [new CommandPaletteEntry("FLIGHT", "Flight", "Flight", false)],
                [new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "VOICE", "Voice", "UI", false)], assignments, new Dictionary<string, string> { [assignments[0].Slot.Key] = "My label" }, sources);
        }
        #endregion END private helpers
    }
}
