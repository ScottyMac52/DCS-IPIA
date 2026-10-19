using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.TestSupport
{
    /// <summary>Creates a portable mixed-brand module with cross-device modifier dependencies in both scopes.</summary>
    public static class MixedSnapshotFixture
    {
        #region BEGIN Public
        /// <summary>Gets the supported controller's stable identity.</summary>
        public static Guid Supported { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        /// <summary>Gets the reserved controller's stable identity.</summary>
        public static Guid Unsupported { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
        /// <summary>Creates independent aircraft/UI Base, shifted and tuned-axis assignments plus device pages.</summary>
        public static EditorWorkspaceSnapshot Create()
        {
            const string aircraft = "d3001pnilu3001cdnilvd1vpnilvunil";
            const string shifted = "d3002pnilu3002cdnilvd1vpnilvunil";
            const string axis = "a2001cdnil";
            const string pause = "d3003pnilu3003cdnilvd1vpnilvunil";
            var module = new DcsModule(Guid.NewGuid(), "MIXED", "Mixed snapshot");
            var profiles = new[] { new DeviceProfile(Supported, "tm-mfd", null, "TM", $"TM {{{Supported:D}}}.diff.lua"),
                new DeviceProfile(Unsupported, "vkb-f14-gunfighter", null, "Other", $"Other {{{Unsupported:D}}}.diff.lua") };
            var config = new ModuleConfiguration(module, profiles, [new DcsModifier("SHIFT", profiles[1].SourceName[..^9], "JOY_BTN3", ModifierMode.Hold)], [], "Snapshot fixture");
            var diagrams = profiles.Select(p => new UiLayerDiagram(p.Role, null, "device.svg", 400, 400, [], [])
            {
                ProfileId = p.Id, Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 400 400'><rect x='100' y='100' width='200' height='200' fill='gray'/></svg>",
                Controls = [new("one", "Button 1", "JOY_BTN1", 10, 10, 120, 30), new("two", "Button 2", "JOY_BTN2", 10, 50, 120, 30),
                    new("three", "Modifier", "JOY_BTN3", 10, 90, 120, 30), new("axis", "Pitch", "JOY_X", 10, 130, 120, 30, true)]
            }).ToArray();
            var assignments = new[] {
                new DraftAssignment(new(module.Id, Supported, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"), new(aircraft, "Base", false, AssignmentLayer.Module)),
                new DraftAssignment(new(module.Id, Supported, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN2", "SHIFT"), new(shifted, "Shifted", false, AssignmentLayer.Module)),
                new DraftAssignment(new(module.Id, Supported, AssignmentLayer.Module, BindingSection.Axis, "JOY_X"), new(axis, "Pitch", true, AssignmentLayer.Module)) { Filter = new AxisFilter(Deadzone: 0.07) },
                new DraftAssignment(new(module.Id, Unsupported, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"), new(aircraft, "Base", false, AssignmentLayer.Module)) };
            var workspace = new EditorWorkspaceSnapshot(config, [], [], diagrams,
                [new(aircraft, "Base", "General", false), new(shifted, "Shifted", "General", false), new(axis, "Pitch", "Axis", true)],
                [new(Guid.NewGuid(), module.Id, pause, "Pause", "General", false)], assignments, new Dictionary<string, string>(),
                new Dictionary<string, string> { ["config/kneeboard.json"] = "{\"includeUiLayer\":true,\"profiles\":{},\"pages\":[],\"summaryPages\":[]}",
                    ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers" });
            foreach (var profile in profiles)
                workspace = WorkspaceStructureEditor.AddDevice(workspace, WorkspaceStructureEditor.Hardware(workspace).Single(h => h.DeviceId == profile.DeviceId),
                    AssignmentLayer.UiLayer, profile.SourceName, profile.Instance, profile.Role);
            workspace = WorkspaceStructureEditor.SetModifier(workspace, null,
                new(AssignmentLayer.UiLayer, Unsupported, "UI_SHIFT", "JOY_BTN3", "UI_SHIFT", ModifierMode.Toggle, profiles[1].SourceName[..^9]));
            workspace = workspace with { Assignments = [.. workspace.Assignments,
                new(new(module.Id, Supported, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"), new(pause, "Pause", false, AssignmentLayer.UiLayer)),
                new(new(module.Id, Supported, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN2", "UI_SHIFT"), new(pause, "Pause", false, AssignmentLayer.UiLayer)),
                new(new(module.Id, Unsupported, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"), new(pause, "Pause", false, AssignmentLayer.UiLayer))] };
            workspace = ModuleKneeboardPageEditor.AddMissingDevicePages(workspace);
            workspace = ModuleKneeboardPageEditor.Set(workspace, null, new(KneeboardPageKind.Summary, null, "overview", "Overview", "CONTROLS", 0, [new("TM", "Supported controls", "cyan")]));
            return workspace with { Labels = workspace.Assignments.ToDictionary(a => a.Slot.Key, a => a.Command.Name + " label", StringComparer.Ordinal) };
        }
        #endregion END Public
    }
}
