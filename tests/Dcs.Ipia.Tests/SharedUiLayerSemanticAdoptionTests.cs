using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises inherited adoption against real rendered profiles and saved revision evidence.</summary>
    public sealed class SharedUiLayerSemanticAdoptionTests
    {
        private const string Root = "assets/shared/ui-layer/";
        private const string ProfilePath = Root + "input/UiLayer/joystick/Stick.diff.lua";
        #region BEGIN Public
        /// <summary>Overrides, clears and custom labels survive while inherited keys, axes and new defaults propagate.</summary>
        [Fact]
        public async Task Propagates_inheritance_and_preserves_module_edits_and_rollback()
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var baseline = await defaults.SaveAsync(Defaults(false));
                var target = await defaults.SaveAsync(Defaults(true) with { Revision = baseline.Revision });
                var module = Module(baseline);
                var original = EditorSnapshotSerializer.Serialize(module);
                var plan = SharedUiLayerSemanticAdoption.Review(module, target, [ProfilePath]);
                var assignments = plan.Workspace.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer).ToArray();
                Assert.Contains(assignments, x => x.Slot.InputKey == "JOY_BTN1" && x.Command.Id == "NEW");
                Assert.Contains(assignments, x => x.Slot.InputKey == "JOY_BTN2" && x.Command.Id == "CUSTOM");
                Assert.DoesNotContain(assignments, x => x.Slot.InputKey is "JOY_BTN3" or "JOY_BTN4");
                Assert.Contains(assignments, x => x.Slot.InputKey == "JOY_BTN5" && x.Command.Id == "NEW");
                Assert.Contains(assignments, x => x.Slot.InputKey == "JOY_X" && x.Filter!.Deadzone == 9);
                Assert.Contains(plan.Changes, x => x.Decision == "Keep module clear");
                Assert.Contains(plan.Changes, x => x.Decision == "Keep module override");
                Assert.Equal("my label", plan.Workspace.Labels[module.Assignments[0].Slot.Key]);
                Assert.Equal(module.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.Module),
                    plan.Workspace.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.Module));
                Assert.Same(module.Configuration, plan.Workspace.Configuration);
                var lua = UiLayerLuaProfileRenderer.Render(plan.Workspace, module.Diagrams[0].ProfileId);
                var rows = DcsLuaParser.ParseProfile(lua, module.Diagrams[0].ProfileId);
                Assert.Contains(rows, x => x.InputKey == "JOY_BTN1" && x.CommandId == "NEW");
                Assert.Contains(rows, x => x.CommandId == "HISTORY" && x.Disposition == BindingDisposition.Removed);
                Assert.Contains(rows, x => x.InputKey == "JOY_BTN99" && x.CommandId == "NEW");
                Assert.Equal(original.Content, EditorSnapshotSerializer.Serialize(module).Content);
                var exported = EditorSnapshotSerializer.Serialize(plan.Workspace);
                Assert.Equal(exported.Content, EditorSnapshotSerializer.Serialize(EditorSnapshotSerializer.Deserialize(exported)).Content);
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(module);
                var expected = (await repository.GetAsync(module.Configuration.Module.Id))!;
                await repository.SaveIfUnchangedAsync(expected, plan.Workspace);
                await repository.SaveIfUnchangedAsync((await repository.GetAsync(module.Configuration.Module.Id))!,
                    (await repository.GetPriorAsync(module.Configuration.Module.Id))!);
                Assert.Equal(original.Content, EditorSnapshotSerializer.Serialize((await repository.GetAsync(module.Configuration.Module.Id))!).Content);
                Assert.Equal(target.ContentSha256, (await defaults.GetAsync())!.ContentSha256);
            }
            finally { File.Delete(file); }
        }
        /// <summary>Catalog labels and modifier mode changes update independently of profile source selections.</summary>
        [Fact]
        public async Task Reviews_catalog_and_modifier_only_updates_and_invalid_choices()
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var baseline = await defaults.SaveAsync(Defaults(false));
                var draft = Defaults(false) with { Revision = baseline.Revision };
                var sources = new Dictionary<string, string>(draft.Sources) {
                    [Root + "functions.json"] = Catalog("Renamed"),
                    [Root + "input/UiLayer/modifiers.lua"] = "local modifiers = { [\"SHIFT\"] = { [\"device\"] = \"Keyboard\", [\"key\"] = \"RShift\", [\"switch\"] = true } }\nreturn modifiers"
                };
                var target = await defaults.SaveAsync(draft with { Sources = sources });
                var module = Module(baseline);
                var plan = SharedUiLayerSemanticAdoption.Review(module, target,
                    [Root + "functions.json", Root + "input/UiLayer/modifiers.lua"]);
                Assert.Equal("Renamed", plan.Workspace.UiCommands.Single(x => x.DcsCommandId == "OLD").Name);
                Assert.Equal("Renamed", plan.Workspace.Assignments.Single(x => x.Slot.InputKey == "JOY_BTN1").Command.Name);
                Assert.Equal("my label", plan.Workspace.Labels[module.Assignments[0].Slot.Key]);
                var modifier = WorkspaceManagement.Read(plan.Workspace)!.Modifiers.Single();
                Assert.Equal(ModifierMode.Toggle, modifier.Mode);
                Assert.Equal("RShift", modifier.Key);
                Assert.Contains(plan.Changes, x => x.Kind == "Modifier" && x.Decision == "Apply inherited change");
                Assert.Contains(plan.Changes, x => x.Kind == "Catalog" && x.Decision == "Apply inherited change");
                Assert.Throws<ArgumentNullException>(() => SharedUiLayerSemanticAdoption.Review(module, target, null!));
                Assert.Throws<ArgumentException>(() => SharedUiLayerSemanticAdoption.Review(module, target, ["not-selected"]));
                Assert.Empty(SharedUiLayerSemanticAdoption.Review(module, target, []).Changes);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Write(IReadOnlyList<DcsBinding> rows)
        {
            var id = rows[0].ProfileId;
            var module = new DcsModule(Guid.NewGuid(), "Test", "Test");
            var config = new ModuleConfiguration(module, [new(id, "stick", null, "stick", "Stick.diff.lua")], [], rows, "test");
            var snapshot = new EditorWorkspaceSnapshot(config, [], [],
                [new("Stick", null, "stick.svg", 100, 100, [], []) { ProfileId = id }], [], [], [], new Dictionary<string, string>());
            return ModuleLuaProfileRenderer.Render(snapshot, id);
        }
        private static string Catalog(string name) => JsonSerializer.Serialize(new { schema = 1, functions = new[] {
            new { id = "old", command = "OLD", label = name, category = "UI" },
            new { id = "new", command = "NEW", label = "New", category = "UI" } } });
        private static SharedUiLayerWorkspace Defaults(bool changed)
        {
            var id = Guid.NewGuid();
            var rows = new List<DcsBinding>();
            foreach (var key in new[] { "JOY_BTN1", "JOY_BTN2", "JOY_BTN3", "JOY_BTN99" })
                rows.Add(new(id, BindingSection.Key, changed ? "NEW" : "OLD", changed ? "New" : "Old", BindingDisposition.Added, key));
            rows.Add(new(id, BindingSection.Key, changed ? "NEW" : "OLD", changed ? "New" : "Old", BindingDisposition.Added, changed ? "JOY_BTN5" : "JOY_BTN4"));
            rows.Add(new(id, BindingSection.Axis, "AXIS", "Axis", BindingDisposition.Added, "JOY_X", [], new(Deadzone: changed ? 9 : 2)));
            rows.Add(new(id, BindingSection.Key, "HISTORY", "History", BindingDisposition.Removed, "JOY_BTN90"));
            return new(0, new string('a', 40), new Dictionary<string, string> {
                [ProfilePath] = Write(rows),
                [Root + "functions.json"] = Catalog("Old"),
                [Root + "input/UiLayer/modifiers.lua"] = "local modifiers = { [\"SHIFT\"] = { [\"device\"] = \"Keyboard\", [\"key\"] = \"LShift\", [\"switch\"] = false } }\nreturn modifiers"
            });
        }
        private static EditorWorkspaceSnapshot Module(SharedUiLayerWorkspace baseline)
        {
            var module = new DcsModule(Guid.NewGuid(), "Test", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "stick", null, "Stick", "Stick.diff.lua");
            var rows = DcsLuaParser.ParseProfile(baseline.Sources[ProfilePath], profile.Id).Where(x => x.InputKey != "JOY_BTN3")
                .Select(x => x.InputKey == "JOY_BTN2" ? new DcsBinding(x.ProfileId, x.Section, "CUSTOM", "Custom", x.Disposition, x.InputKey) : x).ToArray();
            var assignments = rows.Where(x => x.Disposition != BindingDisposition.Removed && x.InputKey != "JOY_BTN99")
                .Select(x => new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.UiLayer, x.Section, x.InputKey, ""),
                    new(x.CommandId, x.CommandName, x.Section == BindingSection.Axis, AssignmentLayer.UiLayer)) { Filter = x.Filter }).ToArray();
            var state = new WorkspaceManagement([new(profile.Id, AssignmentLayer.Module, profile.SourceName, null, "base"),
                new(profile.Id, AssignmentLayer.UiLayer, profile.SourceName, null, "ui-layer")],
                [new(AssignmentLayer.UiLayer, Guid.Empty, "SHIFT", "LShift", "SHIFT", ModifierMode.Hold, "Keyboard")]);
            var sources = new Dictionary<string, string>(baseline.Sources) {
                [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(state),
                ["config/ipia-ui-" + profile.Id.ToString("N") + ".lua"] = Write(rows)
            };
            var controls = rows.Where(x => x.Disposition != BindingDisposition.Removed && x.InputKey != "JOY_BTN99")
                .Select(x => new HardwareGraphicControl(x.InputKey, x.InputKey, x.InputKey, 0, 0, 10, 10, x.Section == BindingSection.Axis))
                .Concat(new[] { new HardwareGraphicControl("JOY_BTN3", "3", "JOY_BTN3", 0, 0, 10, 10),
                    new HardwareGraphicControl("JOY_BTN5", "5", "JOY_BTN5", 0, 0, 10, 10) }).ToArray();
            var catalog = StableIdentity.Create("catalog", "dcs-common-ui-layer");
            return new(new(module, [profile], [], [], "test"), [], [],
                [new("Stick", null, "stick.svg", 100, 100, [], []) { ProfileId = profile.Id, CanonicalUiLayerSourcePath = ProfilePath, Controls = controls }],
                [], [new(StableIdentity.Create("ui-function", "old"), catalog, "OLD", "Old", "UI", false),
                    new(StableIdentity.Create("ui-function", "new"), catalog, "NEW", "New", "UI", false)], assignments,
                new Dictionary<string, string> { [assignments[0].Slot.Key] = "my label" }, sources) { SharedDefault = baseline };
        }
        #endregion END private helpers
    }
}
