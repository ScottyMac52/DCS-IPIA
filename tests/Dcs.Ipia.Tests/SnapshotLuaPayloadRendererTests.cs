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
        private static readonly string[] ReferencedNames = ["ARM_SELECT_1", "ARM_SELECT_2", "LCtrl"];
        #region BEGIN Public
        /// <summary>Case-only module names use the pinned native spelling without changing switch semantics or commands.</summary>
        /// <param name="managed">Whether an older saved workspace has explicit device settings.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReconcilesCaseOnlyNativeDeviceMismatch(bool managed)
        {
            const string oldName = "Viper TQS {C0A33440-3F54-11F1-8001-444553540000}";
            const string nativeName = "Viper TQS {C0A33440-3F54-11f1-8001-444553540000}";
            var saved = Workspace();
            var profile = new DeviceProfile(ProfileId, "tqs", null, "TQS", oldName + ".diff.lua");
            var modifiers = new[] { new DcsModifier("ARM_SELECT_1", oldName, "JOY_BTN55", ModifierMode.Toggle),
                new DcsModifier("ARM_SELECT_2", oldName, "JOY_BTN25", ModifierMode.Toggle),
                new DcsModifier("LCtrl", "Keyboard", "LCtrl", ModifierMode.Hold) };
            var config = new ModuleConfiguration(saved.Configuration.Module, [profile], modifiers, [], "reported package");
            var canonical = "assets/shared/ui-layer/input/UiLayer/joystick/" + nativeName + ".diff.lua";
            var sources = saved.Sources!.ToDictionary(x => x.Key, x => x.Value);
            sources[canonical] = sources[UiPath];
            sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"].Replace("Stick", nativeName, StringComparison.Ordinal);
            if (managed)
                sources[WorkspaceManagement.SourcePath] = System.Text.Json.JsonSerializer.Serialize(new WorkspaceManagement([
                    new(ProfileId, AssignmentLayer.Module, profile.SourceName, null, "TQS"),
                    new(ProfileId, AssignmentLayer.UiLayer, nativeName + ".diff.lua", null, "TQS")
                ], modifiers.Select(x => new WorkspaceModifier(AssignmentLayer.Module,
                    x.Device == "Keyboard" ? Guid.Empty : ProfileId, x.Name, x.Key, x.Name, x.Mode, x.Device)).ToArray()));
            var assignments = modifiers.Select(x => new DraftAssignment(
                new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", x.Name),
                new(x.Name + "_COMMAND", x.Name, false, AssignmentLayer.Module))).ToArray();
            saved = saved with { Configuration = config, Sources = sources, Assignments = assignments,
                Diagrams = [saved.Diagrams[0] with { CanonicalUiLayerSourcePath = canonical }] };
            var output = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("Config/Input/TEST/joystick/" + nativeName + ".diff.lua", output.Keys, StringComparer.Ordinal);
            Assert.DoesNotContain("Config/Input/TEST/joystick/" + oldName + ".diff.lua", output.Keys, StringComparer.Ordinal);
            var exported = DcsLuaParser.ParseModifiers(Text(output, "Config/Input/TEST/modifiers.lua"));
            Assert.Contains(exported, x => x.Name == "ARM_SELECT_1" && x.Device == nativeName && x.Key == "JOY_BTN55" && x.Mode == ModifierMode.Toggle);
            Assert.Contains(exported, x => x.Name == "ARM_SELECT_2" && x.Device == nativeName && x.Key == "JOY_BTN25" && x.Mode == ModifierMode.Toggle);
            Assert.Contains(exported, x => x.Name == "LCtrl" && x.Device == "Keyboard" && x.Mode == ModifierMode.Hold);
            Assert.Equal(modifiers.Select(x => x.Name).Order(StringComparer.Ordinal),
                DcsLuaParser.ParseProfile(Text(output, "Config/Input/TEST/joystick/" + nativeName + ".diff.lua"), ProfileId)
                    .SelectMany(x => x.Reformers).Order(StringComparer.Ordinal));
            Assert.Equal(oldName + ".diff.lua", saved.Configuration.Profiles[0].SourceName);
            Assert.Equal(oldName, saved.Configuration.Modifiers[0].Device);
            // Without an observed native spelling, retain the stored identity instead of guessing.
            var moduleOnly = saved with { Diagrams = [saved.Diagrams[0] with { SupportsUiLayer = false }] };
            if (!managed)
                Assert.Contains("Config/Input/TEST/joystick/" + oldName + ".diff.lua", SnapshotLuaPayloadRenderer.Generate(moduleOnly).Keys, StringComparer.Ordinal);
        }
        /// <summary>Managed exports recover referenced declarations from current and pinned data while retaining exact names, keys, and modes.</summary>
        [Fact]
        public void CompletesDeclarationsForRenderedModifierAssignments()
        {
            var workspace = Workspace();
            var arm1 = new DcsModifier("ARM_SELECT_1", "Stick", "JOY_BTN55", ModifierMode.Toggle);
            var config = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles, [arm1], [], "fixture");
            var state = new WorkspaceManagement([
                new(ProfileId, AssignmentLayer.Module, "Stick.diff.lua", null, "Stick")
            ], []);
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
            sources[WorkspaceManagement.SourcePath] = System.Text.Json.JsonSerializer.Serialize(state);
            sources["src/Config/Input/TEST/modifiers.lua"] = """
                local modifiers = {
                  ["ARM_SELECT_2"] = { ["device"] = "Stick", ["key"] = "JOY_BTN25", ["switch"] = true }
                } return modifiers
                """;
            sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = """
                local modifiers = {
                  ["LCtrl"] = { ["device"] = "Keyboard", ["key"] = "LCtrl", ["switch"] = false }
                } return modifiers
                """;
            var assignments = ReferencedNames.Select(name => new DraftAssignment(
                new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", name),
                new(name + "_COMMAND", name, false, AssignmentLayer.Module))).ToArray();
            var saved = workspace with { Configuration = config, Sources = sources, Assignments = assignments };
            var output = SnapshotLuaPayloadRenderer.Generate(saved);
            var definitions = DcsLuaParser.ParseModifiers(Text(output, "Config/Input/TEST/modifiers.lua"));
            Assert.Contains(arm1, definitions);
            Assert.Contains(definitions, x => x.Name == "ARM_SELECT_2" && x.Key == "JOY_BTN25" && x.Mode == ModifierMode.Toggle);
            Assert.Contains(definitions, x => x.Name == "LCtrl" && x.Device == "Keyboard" && x.Mode == ModifierMode.Hold);
            var rendered = DcsLuaParser.ParseProfile(Text(output, "Config/Input/TEST/joystick/Stick.diff.lua"), ProfileId);
            Assert.Equal(ReferencedNames, rendered.SelectMany(x => x.Reformers).Order(StringComparer.Ordinal));
            Assert.Empty(WorkspaceManagement.Read(saved)!.Modifiers);
            Assert.Single(saved.Configuration.Modifiers);
            Assert.Equal(3, saved.Assignments.Count);
            var explicitModifier = new WorkspaceModifier(AssignmentLayer.Module, ProfileId, arm1.Name, "JOY_BTN56", arm1.Name, ModifierMode.Hold, "Stick");
            sources[WorkspaceManagement.SourcePath] = System.Text.Json.JsonSerializer.Serialize(state with { Modifiers = [explicitModifier] });
            var edited = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains(DcsLuaParser.ParseModifiers(Text(edited, "Config/Input/TEST/modifiers.lua")),
                x => x.Name == arm1.Name && x.Key == "JOY_BTN56" && x.Mode == ModifierMode.Hold);
        }
        /// <summary>Missing or case-mismatched reformers on keys and axes cannot silently produce a broken archive.</summary>
        /// <param name="managed">Whether explicit device management is saved.</param>
        /// <param name="axis">Whether the assignment is an axis.</param>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void RejectsUndeclaredReformers(bool managed, bool axis)
        {
            var workspace = Workspace();
            var section = axis ? BindingSection.Axis : BindingSection.Key;
            var key = axis ? "JOY_X" : "JOY_BTN1";
            var diagram = workspace.Diagrams[0] with {
                SupportsUiLayer = false, Controls = [new("control", "Control", key, 0, 0, 10, 10, axis)]
            };
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
            if (managed)
                sources[WorkspaceManagement.SourcePath] = System.Text.Json.JsonSerializer.Serialize(new WorkspaceManagement([
                    new(ProfileId, AssignmentLayer.Module, "Stick.diff.lua", null, "Stick")
                ], [new(AssignmentLayer.Module, ProfileId, "MOD", "JOY_BTN9", "MOD", ModifierMode.Toggle, "Stick")]));
            var assignment = new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, section, key, "mod"),
                new("COMMAND", "Command", axis, AssignmentLayer.Module));
            var error = Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(
                workspace with { Sources = sources, Diagrams = [diagram], Assignments = [assignment] }));
            Assert.Contains("undeclared modifiers: mod", error.Message);
            Assert.Contains("Stick.diff.lua", error.Message);
        }
        /// <summary>Conflicting pinned definitions are rejected and unused historical reformers remain preserved.</summary>
        [Fact]
        public void RejectsConflictingRecoveryAndPreservesRemovalHistory()
        {
            var workspace = Workspace();
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
            sources["src/Config/Input/TEST/modifiers.lua"] = """
                local modifiers = { ["UNKNOWN"] = { ["device"] = "Stick", ["key"] = "JOY_BTN55", ["switch"] = true } } return modifiers
                """;
            sources["Config/Input/TEST/modifiers.lua"] = sources["src/Config/Input/TEST/modifiers.lua"].Replace("JOY_BTN55", "JOY_BTN25", StringComparison.Ordinal);
            var assignment = new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", "UNKNOWN"),
                new("COMMAND", "Command", false, AssignmentLayer.Module));
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(workspace with { Sources = sources, Assignments = [assignment] }));
            var removed = new DcsBinding(ProfileId, BindingSection.Key, "OLD", "History", BindingDisposition.Removed, "JOY_BTN8", ["UNKNOWN"]);
            var config = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles, workspace.Configuration.Modifiers, [removed], "fixture");
            var output = SnapshotLuaPayloadRenderer.Generate(workspace with { Configuration = config });
            Assert.Contains(DcsLuaParser.ParseProfile(Text(output, "Config/Input/TEST/joystick/Stick.diff.lua"), ProfileId),
                x => x.Disposition == BindingDisposition.Removed && x.Reformers.SequenceEqual(["UNKNOWN"]));
        }
        /// <summary>Previously saved workspaces recover a physical source link without losing edited UI commands.</summary>
        [Fact]
        public void RecoversSavedProfileLinkAndExportsEditedAssignments()
        {
            var original = Workspace();
            var assignment = new DraftAssignment(
                new(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"),
                new("EDITED_UI", "Edited UI", false, AssignmentLayer.UiLayer));
            var saved = original with {
                Diagrams = [original.Diagrams[0] with { CanonicalUiLayerSourcePath = null }],
                Assignments = [..original.Assignments, assignment]
            };
            var recovered = PinnedUiLayerProfileRecovery.Restore(saved);
            Assert.Equal(UiPath, recovered.Diagrams[0].CanonicalUiLayerSourcePath);
            Assert.Same(saved.Assignments, recovered.Assignments);
            Assert.Same(saved.Labels, recovered.Labels);
            Assert.Same(saved.Sources, recovered.Sources);
            Assert.Null(saved.Diagrams[0].CanonicalUiLayerSourcePath);
            Assert.Same(recovered, PinnedUiLayerProfileRecovery.Restore(recovered));
            SnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(saved);
            var output = SnapshotLuaPayloadRenderer.Generate(saved);
            var lua = Text(output, "Config/Input/UiLayer/joystick/Stick.diff.lua");
            Assert.Contains(DcsLuaParser.ParseProfile(lua, ProfileId), x => x.CommandId == "EDITED_UI");
            Assert.Equal(lua, UiLayerLuaProfileRenderer.Render(saved, ProfileId));
        }

        /// <summary>Recovery leaves disabled, unknown, unsafe, and unpinned profiles untouched and names unresolved devices.</summary>
        [Fact]
        public void DoesNotInventMissingProfileLinks()
        {
            var original = Workspace();
            var unpinned = original with { Diagrams = [original.Diagrams[0] with { CanonicalUiLayerSourcePath = null }] };
            var noSources = unpinned with { Sources = null };
            Assert.Same(noSources, PinnedUiLayerProfileRecovery.Restore(noSources));
            var disabled = unpinned with { Diagrams = [unpinned.Diagrams[0] with { SupportsUiLayer = false }] };
            Assert.Same(disabled, PinnedUiLayerProfileRecovery.Restore(disabled));
            var unknown = unpinned with { Diagrams = [unpinned.Diagrams[0] with { ProfileId = Guid.NewGuid() }] };
            Assert.Same(unknown, PinnedUiLayerProfileRecovery.Restore(unknown));
            var sources = new Dictionary<string, string> {
                ["assets/shared/ui-layer/input/UiLayer/joystick/../Stick.diff.lua"] = "return {}",
                ["assets/shared/ui-layer/input/UiLayer/joystick/Other.diff.lua"] = "return {}"
            };
            var missing = unpinned with {
                Sources = sources,
                Assignments = [new DraftAssignment(
                    new(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"),
                    new("UI", "UI", false, AssignmentLayer.UiLayer))]
            };
            Assert.Same(missing, PinnedUiLayerProfileRecovery.Restore(missing));
            var error = Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(missing));
            Assert.Contains("Stick (Stick.diff.lua)", error.Message, StringComparison.Ordinal);
            var orphan = missing with { Assignments = [missing.Assignments[0] with {
                Slot = missing.Assignments[0].Slot with { ProfileId = Guid.NewGuid() }
            }] };
            Assert.Contains(orphan.Assignments[0].Slot.ProfileId.ToString("D"),
                Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(orphan)).Message,
                StringComparison.Ordinal);
        }
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
                workspace with { Sources = workspace.Sources!.Where(x => x.Key != UiPath).ToDictionary(x => x.Key, x => x.Value),
                                 Diagrams = [workspace.Diagrams[0] with { CanonicalUiLayerSourcePath = null }],
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

