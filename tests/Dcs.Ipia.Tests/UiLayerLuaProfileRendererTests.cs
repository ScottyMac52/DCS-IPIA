using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Proves UI Layer Lua reflects saved module edits without dropping unrepresented source bindings.</summary>
    public sealed class UiLayerLuaProfileRendererTests
    {
        private static readonly Guid ModuleId = Guid.Parse("90b0d6c0-a413-42be-93bd-5ab17520c807");
        private static readonly Guid ProfileId = Guid.Parse("990f780c-b9d3-4d39-89a4-dbd7e74006e8");
        private const string Path = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua";
        private const string Modifiers = "assets/shared/ui-layer/input/UiLayer/modifiers.lua";
        #region BEGIN Public
        /// <summary>Explicit selection suppresses unrelated defaults while retaining saved edits and removed history.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExplicitSelectionPreservesEditsWithoutUnselectedDefaults(bool represented)
        {
            var workspace = Workspace([Assigned("EDIT", "Edited", "JOY_BTN1")]);
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
            sources["config/kneeboard.json"] = """{"uiLayerUtilization":{"mode":"explicit","bindings":[]}}""";
            if (!represented) workspace = workspace with { Diagrams = [workspace.Diagrams[0] with { Callouts = [] }] };
            var parsed = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(workspace with { Sources = sources }, ProfileId), ProfileId);
            Assert.Contains(parsed, x => x.CommandId == "EDIT");
            Assert.Contains(parsed, x => x.CommandId == "REMOVED" && x.Disposition == BindingDisposition.Removed);
            Assert.DoesNotContain(parsed, x => x.CommandId == "UNMAPPED");
        }

        /// <summary>Saved edits replace represented slots while unmapped and removed history survive.</summary>
        [Fact]
        public void RendersEditedBindingsAndPreservesUnrepresentedSource()
        {
            var workspace = Workspace([Assigned("EDIT", "Edited", "JOY_BTN1"),
                                       Assigned("SHIFTED", "Shifted", "JOY_BTN2", "SHIFT")]);
            var lua = UiLayerLuaProfileRenderer.Render(workspace, ProfileId);
            Assert.Equal(lua, UiLayerLuaProfileRenderer.Render(workspace, ProfileId));
            var bindings = DcsLuaParser.ParseProfile(lua, ProfileId);
            Assert.Contains(bindings, x => x.CommandId == "EDIT" && x.InputKey == "JOY_BTN1");
            Assert.Contains(bindings, x => x.CommandId == "SHIFTED" && x.Reformers.SequenceEqual(["SHIFT"]));
            Assert.Contains(bindings, x => x.CommandId == "UNMAPPED" && x.InputKey == "JOY_BTN99");
            Assert.Contains(bindings, x => x.CommandId == "REMOVED" && x.Disposition == BindingDisposition.Removed);
            Assert.DoesNotContain(bindings, x => x.CommandId == "ORIGINAL");
        }

        /// <summary>Clearing an imported callout removes it while unrelated canonical source remains.</summary>
        [Fact]
        public void ClearsRepresentedSlotOnly()
        {
            var parsed = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(Workspace([]), ProfileId), ProfileId);
            Assert.DoesNotContain(parsed, x => x.CommandId == "ORIGINAL");
            Assert.Contains(parsed, x => x.CommandId == "UNMAPPED");
            Assert.Contains(parsed, x => x.CommandId == "REMOVED");
        }

        /// <summary>Missing sources, unsafe identities, conflicting edits, and unknown modifiers fail closed.</summary>
        [Fact]
        public void RejectsMissingOrAmbiguousSourcesAndAssignments()
        {
            var workspace = Workspace([Assigned("EDIT", "Edited", "JOY_BTN1")]);
            Assert.Throws<ArgumentException>(() => UiLayerLuaProfileRenderer.Render(workspace, Guid.NewGuid()));
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                workspace with { Sources = null }, ProfileId));
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                workspace with { Diagrams = [workspace.Diagrams[0] with { CanonicalUiLayerSourcePath = "../bad.diff.lua" }] },
                ProfileId));
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                workspace with { Assignments = [.. workspace.Assignments, workspace.Assignments[0]] }, ProfileId));
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                Workspace([Assigned("UNKNOWN", "Unknown", "JOY_BTN1", "NOPE")]), ProfileId));
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                Workspace([Assigned("UNMAPPED", "Unmapped", "JOY_BTN88")]), ProfileId));
        }

        /// <summary>Module-owned modifier removal suppresses canonical bindings and rejects stale edits.</summary>
        [Fact]
        public void AppliesRemovedModifierAndRejectsItsUse()
        {
            var workspace = Workspace([]);
            var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            sources[Path] = Source.Replace("[\"key\"] = \"JOY_BTN99\"", "[\"key\"] = \"JOY_BTN99\", [\"reformers\"] = { [1] = \"SHIFT\" }",
                         StringComparison.Ordinal);
            sources["config/ui-layer-overlay.json"] = """{"removedModifiers":["SHIFT"]}""";
            var removed = workspace with
            {
                Sources = sources,
                Diagrams = [workspace.Diagrams[0] with {
                Callouts = [workspace.Diagrams[0].Callouts[0]]
            }]
            };
            var parsed = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(removed, ProfileId), ProfileId);
            Assert.DoesNotContain(parsed, x => x.CommandId == "UNMAPPED");
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                removed with { Assignments = [Assigned("NEW", "New", "JOY_BTN2", "SHIFT")] }, ProfileId));
        }
        /// <summary>Canonical source paths must retain their pinned root, Lua suffix and input family.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("assets/shared/ui-layer/input/UiLayer/joystick/../bad.diff.lua")]
        [InlineData("assets/shared/ui-layer/input/UiLayer/joystick/bad.lua")]
        [InlineData("assets/shared/ui-layer/input/UiLayer/bad.diff.lua")]
        [InlineData("other/joystick/bad.diff.lua")]
        public void Rejects_invalid_canonical_profile_paths(string? path)
        {
            var workspace = Workspace([]);
            var error = Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(
                workspace with { Diagrams = [workspace.Diagrams[0] with { CanonicalUiLayerSourcePath = path }] }, ProfileId));
            Assert.Contains("No unambiguous pinned UI Layer source", error.Message, StringComparison.Ordinal);
        }

        /// <summary>Imported callouts must reference mapped controls, unique slots and declared modifier chords.</summary>
        [Fact]
        public void Rejects_invalid_imported_callouts()
        {
            var workspace = Workspace([]);
            var callout = workspace.Diagrams[0].Callouts[0];
            foreach (var callouts in new UiLayerCallout[][] {
                [callout with { Control = "missing" }],
                [callout, callout],
                [callout with { Modifier = "UNKNOWN" }]
            })
                Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(workspace with {
                    Diagrams = [workspace.Diagrams[0] with { Callouts = callouts }]
                }, ProfileId));
        }

        /// <summary>Removed modifiers reject invalid arrays and unknown canonical names explicitly.</summary>
        [Theory]
        [InlineData("{\"removedModifiers\":{}}")]
        [InlineData("{\"removedModifiers\":[null]}")]
        [InlineData("{\"removedModifiers\":[\"SHIFT\",\"SHIFT\"]}")]
        [InlineData("{\"removedModifiers\":[\"UNKNOWN\"]}")]
        public void Rejects_invalid_removed_modifier_metadata(string overlay)
        {
            var workspace = Workspace([]);
            var sources = new Dictionary<string, string>(workspace.Sources!) { ["config/ui-layer-overlay.json"] = overlay };
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(workspace with { Sources = sources }, ProfileId));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DraftAssignment Assigned(string id, string name, string key, string chord = "") =>
            new(new(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, key, chord),
                new(id, name, false, AssignmentLayer.UiLayer));
        private static EditorWorkspaceSnapshot Workspace(IReadOnlyList<DraftAssignment> assignments)
        {
            var module = new DcsModule(ModuleId, "TEST", "Test");
            var profile = new DeviceProfile(ProfileId, "stick", null, "Stick", "Stick.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "fixture");
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [],
                [new(1, "one", "Original", "", 0, 0, 10, 10) { CommandId = "ORIGINAL" },
                 new(2, "two", "Shifted", "SHIFT", 10, 0, 10, 10) { CommandId = "SHIFTED" }])
            {
                ProfileId = ProfileId,
                CanonicalUiLayerSourcePath = Path,
                Controls = [new("one", "One", "JOY_BTN1", 0, 0, 10, 10),
                            new("two", "Two", "JOY_BTN2", 10, 0, 10, 10)]
            };
            return new(configuration, [], [], [diagram], [], [], assignments,
                new Dictionary<string, string>(), new Dictionary<string, string>
                {
                    [Path] = Source,
                    [Modifiers] =
                        """local modifiers = { ["SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN3", ["switch"] = false, }, } return modifiers"""
                });
        }
        private const string Source =
            """
            local diff = {
              ["keyDiffs"] = {
                ["ORIGINAL"] = { ["added"] = { [1] = { ["key"] = "JOY_BTN1" }, }, ["name"] = "Original" },
                ["UNMAPPED"] = { ["changed"] = { [1] = { ["key"] = "JOY_BTN99" }, }, ["name"] = "Unmapped" },
                ["REMOVED"] = { ["removed"] = { [1] = { ["key"] = "JOY_BTN3" }, }, ["name"] = "Removed" },
              },
              ["axisDiffs"] = {},
            }
            return diff
            """;
        #endregion END private helpers
    }
}

