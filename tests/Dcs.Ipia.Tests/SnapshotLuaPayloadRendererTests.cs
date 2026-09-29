using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks the complete Lua portion of a saved OvGME payload.</summary>
    public sealed class SnapshotLuaPayloadRendererTests
    {
        private static readonly Guid ModuleId = Guid.Parse("2d4edb94-b44f-49f8-bf22-3382834784a8");
        private static readonly Guid ProfileId = Guid.Parse("569a0227-0901-452c-807a-8da03166eaa3");
        private const string UiPath = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua";
        #region BEGIN Public
        /// <summary>Emits edited profiles and sorted modifier tables from the frozen workspace.</summary>
        [Fact]
        public void GeneratesModuleAndUiLuaWithEffectiveModifiers()
        {
            var workspace = Workspace();
            var output = SnapshotLuaPayloadRenderer.Generate(workspace);
            Assert.Equal(4, output.Count);
            var module = Text(output, "Config/Input/TEST/joystick/Stick.diff.lua");
            Assert.Contains(DcsLuaParser.ParseProfile(module, ProfileId), x => x.CommandId == "EDIT");
            var moduleModifiers = DcsLuaParser.ParseModifiers(Text(output, "Config/Input/TEST/modifiers.lua"));
            Assert.Equal("MOD", Assert.Single(moduleModifiers).Name);
            var ui = Text(output, "Config/Input/UiLayer/joystick/Stick.diff.lua");
            Assert.Contains(DcsLuaParser.ParseProfile(ui, ProfileId), x => x.CommandId == "UI");
            var uiModifiers = DcsLuaParser.ParseModifiers(Text(output, "Config/Input/UiLayer/modifiers.lua"));
            Assert.Equal("SHIFT", Assert.Single(uiModifiers).Name);
            Assert.Equal(output.Keys.Order(StringComparer.OrdinalIgnoreCase),
                         SnapshotLuaPayloadRenderer.Generate(workspace).Keys.Order(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>An overlay removes the modifier and its canonical bindings from both generated files.</summary>
        [Fact]
        public void AppliesUiModifierRemovalToProfileAndTable()
        {
            var workspace = Workspace();
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
            sources["config/ui-layer-overlay.json"] = """{"removedModifiers":["SHIFT"]}""";
            var output = SnapshotLuaPayloadRenderer.Generate(workspace with { Sources = sources });
            Assert.Empty(DcsLuaParser.ParseModifiers(Text(output, "Config/Input/UiLayer/modifiers.lua")));
            Assert.Empty(DcsLuaParser.ParseProfile(Text(output, "Config/Input/UiLayer/joystick/Stick.diff.lua"),
                                                   ProfileId));
        }

        /// <summary>Unpinned edits, duplicate output paths, and malformed paths fail before packaging.</summary>
        [Fact]
        public void RejectsUnresolvedAndCollidingProfiles()
        {
            var workspace = Workspace();
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Sources = null }));
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Diagrams = [workspace.Diagrams[0] with { CanonicalUiLayerSourcePath = null }],
                                 Assignments = [..workspace.Assignments,
                                    new DraftAssignment(
                                        new(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"),
                                        new("UI", "UI", false, AssignmentLayer.UiLayer))] }));
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Diagrams = [workspace.Diagrams[0], workspace.Diagrams[0]] }));
            var other = new DeviceProfile(Guid.NewGuid(), "Other", null, "Other", "Stick.diff.lua");
            var config = new ModuleConfiguration(workspace.Configuration.Module,
                [workspace.Configuration.Profiles[0], other], workspace.Configuration.Modifiers, [], "test");
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Configuration = config }));
        }
        /// <summary>Validates paths and overlay removals before any archive is assembled.</summary>
        [Fact]
        public void RejectsUnsafePathsAndInvalidOverlay()
        {
            var workspace = Workspace();
            var badModule = new ModuleConfiguration(new DcsModule(ModuleId, "../TEST", "Test"),
                workspace.Configuration.Profiles, workspace.Configuration.Modifiers, [], "test");
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Configuration = badModule }));
            var badProfile = new DeviceProfile(ProfileId, "Stick", null, "Stick", "../Stick.diff.lua");
            var badConfig = new ModuleConfiguration(workspace.Configuration.Module, [badProfile],
                workspace.Configuration.Modifiers, [], "test");
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Configuration = badConfig }));
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Diagrams = [workspace.Diagrams[0] with {
                    CanonicalUiLayerSourcePath = "assets/shared/ui-layer/input/UiLayer/other/Stick.diff.lua"
                }] }));
            foreach (var overlay in new[] { """{"removedModifiers":["UNKNOWN"]}""",
                                            """{"removedModifiers":["SHIFT","SHIFT"]}""",
                                            """{"removedModifiers":true}""" })
            {
                var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
                sources["config/ui-layer-overlay.json"] = overlay;
                Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                    workspace with { Sources = sources }));
            }
        }

        /// <summary>Module-only workspaces retain pinned keyboard defaults without exporting UI Layer profiles.</summary>
        [Fact]
        public void GeneratesModuleOnlyPayloadAndEscapesModifierNames()
        {
            var workspace = Workspace();
            var moduleOnly = workspace with {
                Diagrams = [workspace.Diagrams[0] with { SupportsUiLayer = false }],
                Sources = workspace.Sources
            };
            var result = SnapshotLuaPayloadRenderer.Generate(moduleOnly);
            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result.Keys, x => x.Contains("UiLayer", StringComparison.Ordinal));
            var escaped = new DcsModifier("Quote \"Name\"", "Stick", "JOY_BTN9", ModifierMode.Hold);
            var config = new ModuleConfiguration(workspace.Configuration.Module,
                workspace.Configuration.Profiles, [escaped], [], "test");
            var modified = SnapshotLuaPayloadRenderer.Generate(moduleOnly with { Configuration = config });
            Assert.Equal(escaped.Name, Assert.Single(DcsLuaParser.ParseModifiers(
                Text(modified, "Config/Input/TEST/modifiers.lua"))).Name);
        }

        /// <summary>Keyboard defaults come from pinned shared Lua and are added once without importing device modifiers.</summary>
        [Fact]
        public void IncludesCanonicalKeyboardModifiersInModuleOnlyOutput()
        {
            var workspace = Workspace();
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
            var names = new[] { "LAlt", "LCtrl", "LShift", "LWin", "RAlt", "RCtrl", "RShift", "RWin" };
            sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] =
                "local modifiers = {\n" + string.Join("\n", names.Select(name =>
                    $"[\"{name}\"] = {{ [\"device\"] = \"Keyboard\", [\"key\"] = \"{name}\", [\"switch\"] = false }},")) +
                "\n}\nreturn modifiers\n";
            var config = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles,
                [..workspace.Configuration.Modifiers, new DcsModifier("LCtrl", "Keyboard", "LCtrl", ModifierMode.Hold)],
                [], "test");
            var output = SnapshotLuaPayloadRenderer.Generate(workspace with {
                Configuration = config,
                Sources = sources,
                Diagrams = [workspace.Diagrams[0] with { SupportsUiLayer = false }]
            });
            var modifiers = DcsLuaParser.ParseModifiers(Text(output, "Config/Input/TEST/modifiers.lua"));
            Assert.Equal(9, modifiers.Count);
            Assert.All(names, name => Assert.Single(modifiers, item => item.Name == name && item.Device == "Keyboard"));
            Assert.Contains(modifiers, item => item.Name == "MOD");
            Assert.DoesNotContain(output.Keys, path => path.Contains("UiLayer", StringComparison.Ordinal));
        }

        #endregion END Public
        #region BEGIN private helpers
        private static string Text(IReadOnlyDictionary<string, byte[]> output, string path) =>
            Encoding.UTF8.GetString(output[path]);
        private static EditorWorkspaceSnapshot Workspace()
        {
            var profile = new DeviceProfile(ProfileId, "Stick", null, "Stick", "Stick.diff.lua");
            var modifier = new DcsModifier("MOD", "Stick", "JOY_BTN9", ModifierMode.Toggle);
            var config = new ModuleConfiguration(new DcsModule(ModuleId, "TEST", "Test"),
                [profile], [modifier], [], "test");
            var diagram = new UiLayerDiagram("Stick", null, "Stick.svg", 100, 100, [], []) {
                ProfileId = ProfileId, CanonicalUiLayerSourcePath = UiPath,
                Controls = [new("one", "One", "JOY_BTN1", 0, 0, 10, 10)]
            };
            var assignments = new[] {
                new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"),
                                    new("EDIT", "Edit", false, AssignmentLayer.Module))
            };
            var sources = new Dictionary<string, string> {
                [UiPath] = """
                    local diff = { ["keyDiffs"] = {
                      ["UI"] = { ["added"] = { [1] = { ["key"] = "JOY_BTN8",
                        ["reformers"] = { [1] = "SHIFT" } }, }, ["name"] = "UI" },
                    }, ["axisDiffs"] = {} }
                    return diff
                    """,
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] =
                    """local modifiers = { ["SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN3", ["switch"] = false } } return modifiers"""
            };
            return new(config, [], [], [diagram], [], [], assignments, new Dictionary<string, string>(), sources);
        }
        #endregion END private helpers
    }
}
