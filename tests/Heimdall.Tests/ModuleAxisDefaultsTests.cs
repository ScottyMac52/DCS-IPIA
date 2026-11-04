using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks exact default-axis suppression without guessed aircraft command identifiers.</summary>
    public sealed class ModuleAxisDefaultsTests
    {
        private static readonly Guid ModuleId = Guid.Parse("9724b876-2f71-482d-b4da-9ab62ee6bbca");
        private static readonly Guid ProfileId = Guid.Parse("d7ef6b85-89cd-4b3c-b583-94b52aa903b0");
        private const string Defaults = """
            local diff = {
                ["axisDiffs"] = {
                    ["a2001cdnil"] = { ["name"] = "Pitch", ["removed"] = { [1] = { ["key"] = "JOY_X" } } },
                    ["a2002cdnil"] = { ["name"] = "Roll", ["removed"] = { [1] = { ["key"] = "JOY_X" }, [2] = { ["key"] = "JOY_Y" } } },
                    ["a2003cdnil"] = { ["name"] = "Throttle", ["removed"] = { [1] = { ["key"] = "JOY_Z" }, [2] = { ["key"] = "JOY_RZ", ["reformers"] = { [1] = "SHIFT" } } } },
                },
                ["keyDiffs"] = { ["KEY"] = { ["name"] = "Key", ["removed"] = { [1] = { ["key"] = "JOY_BTN1" } } } },
            }
            return diff
            """;
        #region BEGIN Public
        /// <summary>Defaults on nonexistent hardware disappear, with explicit command, axis, chord and filter retained.</summary>
        [Fact]
        public void Render_SuppressesDefaultsByCommandAxisAndChord()
        {
            var workspace = Workspace();
            var assignment = new DcsDraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Axis, "JOY_X"),
                new("a2001cdnil", "Pitch", true, AssignmentLayer.Module)) { Filter = new(Deadzone: 4, Invert: true) };
            workspace = workspace with { Assignments = [assignment] };
            var imported = DcsModuleAxisDefaults.Import(workspace, ProfileId, AssignmentLayer.Module, Defaults);
            Assert.Empty(workspace.Sources!);
            Assert.Equal(workspace.Assignments, imported.Assignments);
            var lua = DcsModuleLuaProfileRenderer.Render(imported, ProfileId);
            Assert.Equal(lua, DcsModuleLuaProfileRenderer.Render(DcsEditorSnapshotSerializer.Deserialize(DcsEditorSnapshotSerializer.Serialize(imported)), ProfileId));
            var bindings = DcsLuaParser.ParseProfile(lua, ProfileId);
            var active = Assert.Single(bindings, x => x.Disposition != BindingDisposition.Removed);
            Assert.Equal("a2001cdnil", active.CommandId);
            Assert.Equal(4d, active.Filter?.Deadzone);
            Assert.True(active.Filter?.Invert);
            Assert.DoesNotContain(bindings, x => x.CommandId == "a2001cdnil" && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(bindings, x => x.CommandId == "a2002cdnil" && x.InputKey == "JOY_X" && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(bindings, x => x.InputKey == "JOY_Y" && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(bindings, x => x.InputKey == "JOY_RZ" && x.Reformers.Single() == "SHIFT");
            Assert.DoesNotContain(bindings, x => x.Section == BindingSection.Key);
            var restored = imported with { Configuration = new(imported.Configuration.Module, imported.Configuration.Profiles, [],
                [new(ProfileId, BindingSection.Axis, "a2001cdnil", "Pitch", BindingDisposition.Removed, "JOY_X")], "test") };
            Assert.DoesNotContain(DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(restored, ProfileId), ProfileId),
                x => x.CommandId == "a2001cdnil" && x.Disposition == BindingDisposition.Removed);
            var shifted = imported with { Assignments = [assignment with { Slot = assignment.Slot with { ModifierChord = "SHIFT" } }] };
            Assert.Contains(DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(shifted, ProfileId), ProfileId),
                x => x.CommandId == "a2001cdnil" && x.Reformers.Count == 0 && x.Disposition == BindingDisposition.Removed);
        }

        /// <summary>Layer/device clears suppress defaults and never copy aircraft defaults into UI Layer or another device.</summary>
        [Fact]
        public void Clear_PreservesDefaultsAndScopeIsolation()
        {
            var workspace = DcsModuleAxisDefaults.Import(Workspace(), ProfileId, AssignmentLayer.Module, Defaults);
            var device = DcsWorkspaceStructureEditor.Describe(workspace).Devices.Single();
            var cleared = DcsWorkspaceStructureEditor.ClearLayerAssignments(workspace, device, "");
            Assert.All(DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(cleared, ProfileId), ProfileId),
                x => Assert.Equal(BindingDisposition.Removed, x.Disposition));
            cleared = DcsWorkspaceStructureEditor.ClearDeviceAssignments(cleared, device);
            Assert.Equal(5, DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(cleared, ProfileId), ProfileId).Count);
            Assert.Empty(DcsModuleAxisDefaults.Apply(workspace, ProfileId, AssignmentLayer.UiLayer, []));
            Assert.Empty(DcsModuleAxisDefaults.Apply(workspace, Guid.NewGuid(), AssignmentLayer.Module, []));
            var existing = DcsModuleAxisDefaults.Review(workspace, ProfileId, AssignmentLayer.Module, Defaults);
            Assert.Equal(5, DcsModuleAxisDefaults.Apply(workspace, ProfileId, AssignmentLayer.Module, existing).Count);
        }

        /// <summary>Unmapped or cleared imported axes become removals while unmapped key history survives.</summary>
        [Theory]
        [InlineData("JOY_X")]
        [InlineData("JOY_RZ")]
        public void Render_RemovesUnassignedImportedAxes(string axis)
        {
            var workspace = Workspace();
            var configuration = workspace.Configuration;
            workspace = workspace with { Configuration = new(configuration.Module, configuration.Profiles, [],
                [new(ProfileId, BindingSection.Axis, "a2001cdnil", "Pitch", BindingDisposition.Changed, axis),
                 new(ProfileId, BindingSection.Key, "KEY", "Key", BindingDisposition.Added, "JOY_BTN99")], "test") };
            var bindings = DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(workspace, ProfileId), ProfileId);
            Assert.Contains(bindings, x => x.InputKey == axis && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(bindings, x => x.InputKey == "JOY_BTN99" && x.Disposition == BindingDisposition.Added);
            var cleared = DcsWorkspaceStructureEditor.ClearDeviceAssignments(workspace, DcsWorkspaceStructureEditor.Describe(workspace).Devices.Single());
            Assert.Equal(BindingDisposition.Removed, Assert.Single(DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(cleared, ProfileId), ProfileId)).Disposition);
        }

        /// <summary>Clearing a chord removes its authored axis and preserves the other chord.</summary>
        [Fact]
        public void Clear_AssignedAxisRetainsRemovalForSelectedChord()
        {
            var workspace = Workspace();
            workspace = workspace with { Configuration = new(workspace.Configuration.Module, workspace.Configuration.Profiles,
                [new("SHIFT", "Keyboard", "LShift", ModifierMode.Hold)], [], "test") };
            var command = new DcsDraftCommand("a2001cdnil", "Pitch", true, AssignmentLayer.Module);
            var slot = new DcsDraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Axis, "JOY_X");
            workspace = workspace with { Assignments = [new(slot, command), new(slot with { ModifierChord = "SHIFT" }, command)] };
            var device = DcsWorkspaceStructureEditor.Describe(workspace).Devices.Single();
            var cleared = DcsWorkspaceStructureEditor.ClearLayerAssignments(workspace, device, "SHIFT");
            var bindings = DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(cleared, ProfileId), ProfileId);
            Assert.Contains(bindings, x => x.Reformers.Count == 0 && x.Disposition == BindingDisposition.Added);
            Assert.Contains(bindings, x => x.Reformers.SingleOrDefault() == "SHIFT" && x.Disposition == BindingDisposition.Removed);
            cleared = DcsWorkspaceStructureEditor.ClearDeviceAssignments(cleared, device);
            Assert.All(DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(cleared, ProfileId), ProfileId),
                x => Assert.Equal(BindingDisposition.Removed, x.Disposition));
        }

        /// <summary>Managed UI axes keep their own captured defaults and clearing history without inheriting aircraft evidence.</summary>
        [Fact]
        public void UiLayer_UsesItsOwnBaselineAndPreservesAssignedAxes()
        {
            var workspace = Workspace();
            var hardware = new WorkspaceHardware("Stick", "Stick", "Stick.diff.lua", workspace.Diagrams[0]);
            workspace = DcsWorkspaceStructureEditor.AddDevice(workspace, hardware, AssignmentLayer.UiLayer, "Stick.diff.lua", null, null);
            var device = DcsWorkspaceStructureEditor.Describe(workspace).Devices.Single(x => x.Layer == AssignmentLayer.UiLayer);
            workspace = DcsModuleAxisDefaults.Import(workspace, device.ProfileId, AssignmentLayer.UiLayer, Defaults);
            var command = new DcsDraftCommand("a2001cdnil", "Pitch", true, AssignmentLayer.UiLayer);
            workspace = workspace with { Assignments = [new(new(ModuleId, device.ProfileId, AssignmentLayer.UiLayer, BindingSection.Axis, "JOY_X"), command)] };
            var bindings = DcsLuaParser.ParseProfile(DcsUiLayerLuaProfileRenderer.Render(workspace, device.ProfileId), device.ProfileId);
            Assert.Equal("a2001cdnil", Assert.Single(bindings, x => x.Disposition != BindingDisposition.Removed).CommandId);
            Assert.Equal(4, bindings.Count(x => x.Disposition == BindingDisposition.Removed));
            Assert.Empty(DcsLuaParser.ParseProfile(DcsModuleLuaProfileRenderer.Render(workspace, ProfileId), ProfileId));
            var cleared = DcsWorkspaceStructureEditor.ClearLayerAssignments(workspace, device, "");
            Assert.All(DcsLuaParser.ParseProfile(DcsUiLayerLuaProfileRenderer.Render(cleared, device.ProfileId), device.ProfileId),
                x => Assert.Equal(BindingDisposition.Removed, x.Disposition));
        }

        /// <summary>Invalid files, active axes and duplicate combinations cannot stage a baseline.</summary>
        [Theory]
        [InlineData("local diff = {} return diff")]
        [InlineData("local diff = { [\"axisDiffs\"] = { [\"a2001cdnil\"] = { [\"name\"] = \"Pitch\", [\"added\"] = { [1] = { [\"key\"] = \"JOY_X\" } } } } } return diff")]
        [InlineData("local diff = { [\"axisDiffs\"] = { [\"a2001cdnil\"] = { [\"name\"] = \"Pitch\", [\"removed\"] = { [1] = { [\"key\"] = \"JOY_X\" }, [2] = { [\"key\"] = \"JOY_X\" } } } } } return diff")]
        public void Import_RejectsInvalidEvidenceWithoutMutation(string source)
        {
            var workspace = Workspace();
            Assert.Throws<InvalidDataException>(() => DcsModuleAxisDefaults.Import(workspace, ProfileId, AssignmentLayer.Module, source));
            Assert.Empty(workspace.Sources!);
        }

        /// <summary>A baseline cannot target a device or scope outside the selected module.</summary>
        [Fact]
        public void Review_RejectsUnknownDestinationAndMalformedLua()
        {
            Assert.Throws<InvalidDataException>(() => DcsModuleAxisDefaults.Review(Workspace(), Guid.NewGuid(), AssignmentLayer.Module, Defaults));
            Assert.Throws<InvalidDataException>(() => DcsModuleAxisDefaults.Review(Workspace(), ProfileId, AssignmentLayer.UiLayer, Defaults));
            Assert.Throws<FormatException>(() => DcsModuleAxisDefaults.Review(Workspace(), ProfileId, AssignmentLayer.Module, "not a profile"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsEditorWorkspaceSnapshot Workspace()
        {
            var configuration = new DcsModuleConfiguration(new(ModuleId, "AXIS-TEST", "Axis test"),
                [new(ProfileId, "Stick", null, "Stick", "Stick.diff.lua")], [], [], "test");
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [], []) {
                ProfileId = ProfileId, Controls = [new("x", "X", "JOY_X", 0, 0, 10, 10, true)] };
            return new(configuration, [], [], [diagram], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string>());
        }
        #endregion END private helpers
    }
}
