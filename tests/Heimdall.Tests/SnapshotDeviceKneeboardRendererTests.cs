using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.Rendering;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks complete device PNG output from saved assignments and pinned SVGs.</summary>
    public sealed class SnapshotDeviceKneeboardRendererTests
    {
        private static readonly Guid ModuleId = Guid.Parse("fc35b653-ea6d-4ec6-99d3-bcb22eb5d8d2");
        private static readonly Guid ProfileId = Guid.Parse("529897fc-ab18-495d-97e6-1d23fac78c86");
        #region BEGIN Public
        /// <summary>One module and UI Layer image responds to a saved label while ignoring imported PNG bytes.</summary>
        [Fact]
        public void GeneratesCombinedDevicePngFromSavedLabels()
        {
            var workspace = Workspace();
            var first = SnapshotDeviceKneeboardRenderer.Generate(workspace);
            var bytes = Assert.Single(first).Value;
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
            Assert.Contains("KNEEBOARD/TEST/01-DEVICE.png", first.Keys);
            Assert.Equal(bytes, Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(workspace)).Value);
            var slot = workspace.Assignments[0].Slot.Key;
            var changed =
                workspace with { Labels = new Dictionary<string, string> { [slot] = "Different saved callout" } };
            var changedPage = KneeboardDraftProjection.CreateCombined(changed, changed.Diagrams[0], changed.Assignments,
                                                                      changed.Labels);
            Assert.Equal("Different saved callout", changedPage.Labels["one"][0].Text);
            Assert.Contains("Different saved callout",
                            CompositeKneeboardRenderer.BuildDeviceSvg(changed.Diagrams[0], changedPage),
                            StringComparison.Ordinal);
            Assert.NotEqual(bytes, Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(changed)).Value);
            var page = KneeboardDraftProjection.CreateCombined(workspace, workspace.Diagrams[0], workspace.Assignments,
                                                               workspace.Labels);
            var svg = CompositeKneeboardRenderer.BuildPageSvg(workspace.Diagrams[0], page, "");
            Assert.Contains("data-modifier-legend", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("current draft preview", svg, StringComparison.Ordinal);
        }

        /// <summary>Missing pinned manifest, page mapping, or graphic prevents a partial package.</summary>
        [Fact]
        public void RequiresPinnedAuthoredPagesAndGraphic()
        {
            var workspace = Workspace();
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(workspace with { Sources = null }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(workspace with { Pages = [] }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(workspace with { Diagrams = [] }));
            Assert.Throws<InvalidDataException>(() => SnapshotDeviceKneeboardRenderer.Generate(workspace with {
                Sources =
                    new Dictionary<string, string> { ["config/kneeboard.json"] = "{\"profiles\":{},\"pages\":[]}" }
            }));
        }

        /// <summary>Unmapped and duplicate saved slots are rejected instead of omitting their labels.</summary>
        [Fact]
        public void RejectsUnrepresentedSavedAssignments()
        {
            var workspace = Workspace();
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    workspace with { Assignments = [..workspace.Assignments, workspace.Assignments[0]] }));
            var bad =
                workspace.Assignments[0] with { Slot = workspace.Assignments[0].Slot with { InputKey = "JOY_BTN99" } };
            Assert.Throws<InvalidDataException>(() => SnapshotDeviceKneeboardRenderer.Generate(
                                                    workspace with { Assignments = [bad, workspace.Assignments[1]] }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    workspace with { Diagrams = [workspace.Diagrams[0] with { SupportsUiLayer = false }] }));
        }
        /// <summary>Base and shifted POV spellings in one profile map to their actual DCS keys.</summary>
        [Fact]
        public void ResolvesMixedPovSpellingsByModifierChord()
        {
            var workspace = Workspace();
            var bindings = new[] {
                new DcsBinding(ProfileId, BindingSection.Key, "trim", "Trim", BindingDisposition.Added,
                               "JOY_BTN_POV1_D"),
                new DcsBinding(ProfileId, BindingSection.Key, "view", "View", BindingDisposition.Added,
                               "JOY_POV1_D", ["SHIFT"])
            };
            workspace = workspace with {
                Configuration = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles,
                    [new DcsModifier("SHIFT", "Device", "JOY_BTN3", ModifierMode.Hold)], bindings, "fixture")
            };
            var baseControl = new HardwareGraphicControl("trim", "Trim", "JOY_POV1_D", 0, 0, 10, 10);
            var diagram = workspace.Diagrams[0] with { Controls = [baseControl] };
            Assert.Equal("JOY_BTN_POV1_D",
                         KneeboardDraftProjection.SlotForControl(workspace, diagram, baseControl,
                             AssignmentLayer.Module, "")?.InputKey);
            Assert.Equal("JOY_POV1_D",
                         KneeboardDraftProjection.SlotForControl(workspace, diagram, baseControl,
                             AssignmentLayer.Module, "SHIFT")?.InputKey);
        }

        /// <summary>Ambiguous identities, extra pages and unsafe authored paths cannot make a partial export.</summary>
        [Fact]
        public void RejectsAmbiguousAndUnsafePageMappings()
        {
            var workspace = Workspace();
            var source = workspace.Sources!["config/kneeboard.json"];
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    WithManifest(workspace, source.Replace("{\"stick\":\"Device.diff.lua\"}",
                                                           "{\"a\":\"Device.diff.lua\",\"b\":\"Device.diff.lua\"}",
                                                           StringComparison.Ordinal))));
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    WithManifest(workspace, source.Replace("\"file\":\"01-DEVICE\"", "\"file\":\"../01-DEVICE\"",
                                                           StringComparison.Ordinal))));
            Assert.Throws<InvalidDataException>(() => SnapshotDeviceKneeboardRenderer.Generate(workspace with {
                Pages = [..workspace.Pages, workspace.Pages[0] with { StableKey = "02-ORPHAN" }]
            }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    workspace with { Pages = [..workspace.Pages, workspace.Pages[0]] }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    workspace with { Diagrams = [workspace.Diagrams[0] with { Controls = [] }] }));
        }
        /// <summary>A pinned font makes missing characters an explicit export error rather than an invisible label.</summary>
        [Fact]
        public void RejectsGlyphsOutsidePinnedFont()
        {
            var workspace = Workspace();
            var slot = workspace.Assignments[0].Slot.Key;
            Assert.Throws<InvalidDataException>(
                () => SnapshotDeviceKneeboardRenderer.Generate(
                    workspace with { Labels = new Dictionary<string, string> { [slot] = "Unsupported 🚀" } }));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot WithManifest(EditorWorkspaceSnapshot workspace, string manifest) =>
            workspace with { Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = manifest } };
        private static EditorWorkspaceSnapshot Workspace()
        {
            var module = new DcsModule(ModuleId, "TEST", "Test module");
            var profile = new DeviceProfile(ProfileId, "device", null, "Device", "Device.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "fixture");
            var page = new KneeboardPage(Guid.NewGuid(), ModuleId, Guid.NewGuid(), KneeboardPageKind.Device,
                                         "01-DEVICE", "Device", [], 1);
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) {
                ProfileId = ProfileId, Controls = [new("one", "One", "JOY_BTN1", 0, 0, 10, 10)],
                Svg =
                    "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='20' y='20' fill='white'>Old</text></svg>"
            };
            var moduleSlot =
                new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            var uiSlot = new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key,
                                                 "JOY_BTN1", "SHIFT");
            var assignments =
                new[] { new DraftAssignment(moduleSlot, new("M", "Module action", false, AssignmentLayer.Module)),
                        new DraftAssignment(uiSlot, new("U", "UI action", false, AssignmentLayer.UiLayer)) };
            const string source =
                "{\"profiles\":{\"stick\":\"Device.diff.lua\"},\"pages\":[{\"profile\":\"stick\",\"file\":\"01-DEVICE\",\"title\":\"Device\"}]}";
            return new(configuration, [page],
                       [new KneeboardImageAsset(page.StableKey, "old.png", [1, 2, 3], "ignored")], [diagram], [], [],
                       assignments, new Dictionary<string, string> { [moduleSlot.Key] = "Saved base label" },
                       new Dictionary<string, string> { ["config/kneeboard.json"] = source });
        }
        #endregion END private helpers
    }
}
