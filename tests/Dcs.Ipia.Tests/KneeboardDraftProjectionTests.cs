using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks current-draft kneeboard pages across device scopes and newly used layers.</summary>
    public sealed class KneeboardDraftProjectionTests
    {
        #region BEGIN Public
        /// <summary>Toggle activators are labeled at their physical positions even without command bindings.</summary>
        [Fact]
        public void LabelsTogglePositionsWithoutReplacingCommandsOrAuthoredText()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "throttle", null, "Throttle", "Throttle {ABCD}.diff.lua");
            var config = new ModuleConfiguration(module, [profile], [
                new DcsModifier("ARM_SELECT_1", "Throttle {abcd}", "JOY_BTN55", ModifierMode.Toggle),
                new DcsModifier("ARM_SELECT_2", "Throttle {ABCD}", "JOY_BTN25", ModifierMode.Toggle)
            ], [], "fixture");
            var diagram = new UiLayerDiagram("Throttle", null, "device.svg", 100, 100, [], []) {
                ProfileId = profile.Id, SupportsUiLayer = false, Controls = [
                    new("bit", "Button 55 — PROGRAM BIT", "JOY_BTN55", 0, 0, 10, 10),
                    new("one", "Button 25 — PROGRAM 1", "JOY_BTN25", 0, 10, 10, 10),
                    new("action", "Action", "JOY_BTN29", 0, 20, 10, 10),
                    new("axis", "Axis", "JOY_X", 0, 30, 10, 10)
                ]
            };
            var snapshot = new EditorWorkspaceSnapshot(config, [], [], [diagram], [], [], [], new Dictionary<string, string>(),
                new Dictionary<string, string> { ["config/kneeboard.json"] =
                    """{"profiles":{"throttle":"Throttle {ABCD}.diff.lua"},"pages":[{"profile":"throttle","labels":{"bit":"Physical selector"}}]}""" });
            var assignments = new[] {
                new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN29", "ARM_SELECT_1"),
                    new("ACTION", "Action", false, AssignmentLayer.Module)),
                new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN25", ""),
                    new("BASE", "Base action", false, AssignmentLayer.Module))
            };
            var page = KneeboardDraftProjection.CreateCombined(snapshot, diagram, assignments, snapshot.Labels);
            Assert.Equal(["Physical selector", "ARM_SELECT_1 (Toggle)"], page.Labels["bit"].Select(x => x.Text));
            Assert.Equal(["Base action", "ARM_SELECT_2 (Toggle)"], page.Labels["one"].Select(x => x.Text));
            Assert.Equal("#dc2626", page.Labels["bit"][1].Color);
            Assert.Contains(page.Legend, x => x.Label == "ARM_SELECT_1 — Button 55 — PROGRAM BIT [JOY_BTN55, Toggle]" && x.Color == "#dc2626");
            Assert.Contains(page.Legend, x => x.Label == "ARM_SELECT_2 — Button 25 — PROGRAM 1 [JOY_BTN25, Toggle]");
            Assert.DoesNotContain("axis", page.Labels.Keys);
            Assert.Empty(snapshot.Assignments);
            Assert.Equal(assignments[1].Command.Name, "Base action");
            var renamed = new ModuleConfiguration(module, [profile], [
                new DcsModifier("NEW_SELECTOR", "Throttle {ABCD}", "JOY_BTN25", ModifierMode.Hold)
            ], [], "fixture");
            var edited = KneeboardDraftProjection.CreateCombined(snapshot with { Configuration = renamed }, diagram, [], snapshot.Labels);
            Assert.Equal("NEW_SELECTOR (Hold)", Assert.Single(edited.Labels["one"]).Text);
            Assert.DoesNotContain(edited.Legend, x => x.Label.Contains("ARM_SELECT", StringComparison.Ordinal));
        }
        /// <summary>Managed UI modifiers resolve POV aliases and cross-device activators without labeling unrelated or shifted controls.</summary>
        [Fact]
        public void DescribesManagedModifiersAcrossDevicesAndScopes()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var target = new DeviceProfile(Guid.NewGuid(), "target", null, "Target", "Target.diff.lua");
            var activator = new DeviceProfile(Guid.NewGuid(), "activator", null, "Activator", "Activator.diff.lua");
            var config = new ModuleConfiguration(module, [target, activator], [], [], "fixture");
            var targetDiagram = new UiLayerDiagram("Target", null, "target.svg", 100, 100, [], []) {
                ProfileId = target.Id, Controls = [new("action", "Action", "JOY_BTN1", 0, 0, 10, 10)]
            };
            var activatorDiagram = new UiLayerDiagram("Activator", null, "activator.svg", 100, 100, [], []) {
                ProfileId = activator.Id, Controls = [
                    new("hat", "Hat up", "JOY_POV1_U", 0, 0, 10, 10),
                    new("hat-shifted", "Hat up shifted", "JOY_POV1_U", 0, 10, 10, 10)
                ]
            };
            var state = new WorkspaceManagement([], [
                new(AssignmentLayer.Module, activator.Id, "MODULE", "JOY_BTN_POV1_U", "Module", ModifierMode.Hold, "Activator"),
                new(AssignmentLayer.UiLayer, activator.Id, "UI", "JOY_BTN_POV1_U", "UI", ModifierMode.Toggle, "Activator"),
                new(AssignmentLayer.Module, Guid.Empty, "UNRESOLVED", "K", "Unresolved", ModifierMode.Hold, "Keyboard")
            ]);
            var snapshot = new EditorWorkspaceSnapshot(config, [], [], [targetDiagram, activatorDiagram], [], [], [], new Dictionary<string, string>(),
                new Dictionary<string, string> { [WorkspaceManagement.SourcePath] = System.Text.Json.JsonSerializer.Serialize(state) });
            var assigned = new DraftAssignment(new(module.Id, target.Id, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1", "UI"),
                new("ACTION", "Action", false, AssignmentLayer.UiLayer));
            var page = KneeboardDraftProjection.CreateCombined(snapshot, targetDiagram, [assigned], snapshot.Labels);
            Assert.Contains(page.Legend, x => x.Label == "UI Layer — UI — Activator / Hat up [JOY_BTN_POV1_U, Toggle]");
            Assert.DoesNotContain(page.Labels.Values.SelectMany(x => x), x => x.Text.Contains("(Toggle)", StringComparison.Ordinal));
            var own = KneeboardDraftProjection.CreateCombined(snapshot, activatorDiagram, [], snapshot.Labels);
            Assert.Equal(["MODULE (Hold)", "UI: UI (Toggle)"], own.Labels["hat"].Select(x => x.Text));
            Assert.DoesNotContain("hat-shifted", own.Labels.Keys);
            Assert.DoesNotContain(own.Legend, x => x.Label.Contains("UNRESOLVED", StringComparison.Ordinal));
            var moduleAssignment = assigned with { Slot = assigned.Slot with { Layer = AssignmentLayer.Module, ModifierChord = "UNRESOLVED" },
                Command = assigned.Command with { Layer = AssignmentLayer.Module } };
            page = KneeboardDraftProjection.CreateCombined(snapshot, targetDiagram, [moduleAssignment], snapshot.Labels);
            Assert.Contains(page.Legend, x => x.Label == "UNRESOLVED — Keyboard / K [K, Hold]");
        }
        /// <summary>A new shift assignment gets its own page while Base and UI Layer retain their independent labels.</summary>
        [Fact]
        public void Renders_every_used_layer_from_pending_assignments()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "test-device", null, "Device", "Device.diff.lua");
            var configuration = new ModuleConfiguration(
                module, [profile], [new DcsModifier("SHIFT", "test-device", "JOY_BTN7", ModifierMode.Hold)], [],
                "fixture");
            var controls = new[] { new HardwareGraphicControl("one", "One", "JOY_BTN1", 10, 10, 40, 20),
                                   new HardwareGraphicControl("two", "Two", "JOY_BTN2", 10, 40, 40, 20) };
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [],
                                             []) { ProfileId = profile.Id, Controls = controls, Svg = "<svg/>" };
            var snapshot = new EditorWorkspaceSnapshot(configuration, [], [], [diagram], [], [], [],
                                                       new Dictionary<string, string>());
            DraftAssignment Assigned(AssignmentLayer layer, string input, string id, string chord = "") =>
                new(new(module.Id, profile.Id, layer, BindingSection.Key, input, chord),
                    new DraftCommand(id, id, false, layer));
            var original = Assigned(AssignmentLayer.Module, "JOY_BTN1", "BASE");
            var shifted = Assigned(AssignmentLayer.Module, "JOY_BTN2", "SHIFTED", "SHIFT");
            var ui = Assigned(AssignmentLayer.UiLayer, "JOY_BTN1", "UI");
            var labels = new Dictionary<string, string> { [shifted.Slot.Key] = "New shifted label" };
            var pages = KneeboardDraftProjection.Create(snapshot, diagram, [original, shifted, ui], labels);
            Assert.Equal(["Module · Base", "Module · SHIFT", "UI Layer · Base"], pages.Select(x => x.Title));
            Assert.Equal("BASE", pages[0].Captions["one"]);
            Assert.Equal("Unassigned", pages[0].Captions["two"]);
            Assert.Equal("New shifted label", pages[1].Captions["two"]);
            Assert.Equal("UI", pages[2].Captions["one"]);
            Assert.Equal("Unassigned", pages[2].Captions["two"]);
            Assert.DoesNotContain(pages, x => x.Title == "UI Layer · SHIFT");
            var combined = KneeboardDraftProjection.CreateCombined(snapshot, diagram, [original, shifted, ui], labels);
            Assert.Equal(["BASE", "UI"], combined.Labels["one"].Select(x => x.Text));
            Assert.Equal(["New shifted label"], combined.Labels["two"].Select(x => x.Text));
            Assert.Contains(combined.Legend, x => x.Label.Contains("SHIFT", StringComparison.Ordinal) &&
                x.Label.Contains("JOY_BTN7", StringComparison.Ordinal) && x.Label.Contains("Hold", StringComparison.Ordinal) && x.Color == "#dc2626");
            Assert.Contains(combined.Legend, x => x.Label == "UI Layer — Base" && x.Color == "#0891b2");
        }
        /// <summary>An unsupported UI Layer produces only module pages even when a stale UI assignment exists.</summary>
        [Fact]
        public void Unsupported_ui_layer_is_not_rendered()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "device", null, "Device", "Device.diff.lua");
            var config = new ModuleConfiguration(module, [profile], [], [], "fixture");
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) {
                ProfileId = profile.Id, SupportsUiLayer = false,
                Controls = [new HardwareGraphicControl("one", "One", "JOY_BTN1", 10, 10, 40, 20)]
            };
            var snapshot =
                new EditorWorkspaceSnapshot(config, [], [], [diagram], [], [], [], new Dictionary<string, string>());
            var pages = KneeboardDraftProjection.Create(snapshot, diagram, [], new Dictionary<string, string>());
            Assert.Single(pages);
            Assert.Equal("Module · Base", pages[0].Title);
        }
        /// <summary>One authored page retains static control text and category names while staged module and UI assignments share it.</summary>
        [Fact]
        public void Combined_device_uses_authored_page_and_current_ui_assignments()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "test-device", null, "Device", "Device.diff.lua");
            var config = new ModuleConfiguration(module, [profile], [], [], "fixture");
            var diagram = new UiLayerDiagram(
                "Device", null, "device.svg", 100, 100, [],
                []) { ProfileId = profile.Id, Controls = [
                         new HardwareGraphicControl("one", "One", "JOY_BTN1", 10, 10, 40, 20),
                         new HardwareGraphicControl("one-shifted", "One shifted", "JOY_BTN1", 10, 40, 40, 20),
                         new HardwareGraphicControl("two", "Two", "JOY_BTN2", 10, 70, 40, 20)
                     ] };
            const string source = """
               {"profiles":{"stick":"Device.diff.lua"},"pages":[
                {"profile":"stick","title":"Authored device","kicker":"FLIGHT CONTROLS",
                 "labels":{"two":"Physical control"},"categoryLabels":{"top":"Flight"},
                 "layers":[{"labels":{"one":"Old authored label"},"categoryLabels":{"right":"Systems"}}]}
               ]}
               """;
            var snapshot =
                new EditorWorkspaceSnapshot(config, [], [], [diagram], [], [], [], new Dictionary<string, string>(),
                                            new Dictionary<string, string> { ["config/kneeboard.json"] = source });
            DraftAssignment Assigned(AssignmentLayer layer, string id, string chord = "") =>
                new(new(module.Id, profile.Id, layer, BindingSection.Key, "JOY_BTN1", chord),
                    new DraftCommand(id, id, false, layer));
            var moduleBase = Assigned(AssignmentLayer.Module, "Module");
            var uiShift = Assigned(AssignmentLayer.UiLayer, "UI shifted", "UI_BTN7");
            var overrides = new Dictionary<string, string> { [moduleBase.Slot.Key] = "Edited module" };
            var page = KneeboardDraftProjection.CreateCombined(snapshot, diagram, [moduleBase, uiShift], overrides);
            Assert.Equal("Authored device", page.Title);
            Assert.Equal("FLIGHT CONTROLS", page.Kicker);
            Assert.Equal("Edited module", Assert.Single(page.Labels["one"]).Text);
            Assert.Equal("UI shifted", Assert.Single(page.Labels["one-shifted"]).Text);
            Assert.Equal("Physical control", Assert.Single(page.Labels["two"]).Text);
            Assert.Equal("Flight", page.CategoryLabels["mfd-category-top"]);
            Assert.Equal("Systems", page.CategoryLabels["mfd-category-right"]);
            Assert.Contains(page.Legend, x => x.Label == "UI Layer — UI_BTN7");
        }
        #endregion END Public
    }
}
