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
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Propagates_inheritance_and_preserves_module_edits_and_rollback(bool managed)
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            var connection = "Data Source=" + file + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var baseline = await defaults.SaveAsync(Defaults(false));
                var target = await defaults.SaveAsync(Defaults(true) with { Revision = baseline.Revision });
                var module = Module(baseline, managed);
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
                    [Root + "input/UiLayer/modifiers.lua"] = "local modifiers = { [\"SHIFT\"] = { [\"device\"] = \"Keyboard\", [\"key\"] = \"RShift\", [\"switch\"] = true }, [\"UNRELATED\"] = { [\"device\"] = \"Other\", [\"key\"] = \"JOY_BTN7\", [\"switch\"] = false } }\nreturn modifiers"
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
                Assert.Contains(plan.Changes, x => x.Identity == "UNRELATED" && x.Decision == "Keep absent device");
                Assert.Contains(plan.Changes, x => x.Kind == "Catalog" && x.Decision == "Apply inherited change");
                Assert.Throws<ArgumentNullException>(() => SharedUiLayerSemanticAdoption.Review(module, target, null!));
                Assert.Throws<ArgumentException>(() => SharedUiLayerSemanticAdoption.Review(module, target, ["not-selected"]));
                Assert.Empty(SharedUiLayerSemanticAdoption.Review(module, target, []).Changes);
            }
            finally { File.Delete(file); }
        }
        /// <summary>Filename, GUID and shared-hardware name fallbacks apply defaults without losing module overrides.</summary>
        [Theory]
        [InlineData("Stick.diff.lua", "Stick.diff.lua")]
        [InlineData("STICK.diff.lua", "Stick.diff.lua")]
        [InlineData("Renamed {11111111-1111-1111-1111-111111111111}.diff.lua", "Stick {11111111-1111-1111-1111-111111111111}.diff.lua")]
        [InlineData("Stick {11111111-1111-1111-1111-111111111111}.diff.lua", "Stick {22222222-2222-2222-2222-222222222222}.diff.lua")]
        [InlineData("Renamed.diff.lua", "Stick {22222222-2222-2222-2222-222222222222}.diff.lua")]
        public async Task Resolves_reviewed_profile_fallbacks(string moduleFile, string sharedFile)
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            try
            {
                var connection = "Data Source=" + file + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var baseline = await repository.SaveAsync(Defaults(false));
                var module = WithFallbackIdentity(Module(baseline), moduleFile);
                var draft = Defaults(true);
                var sources = new Dictionary<string, string>(draft.Sources);
                var lua = sources[ProfilePath];
                sources.Remove(ProfilePath);
                var path = Root + "input/UiLayer/joystick/" + sharedFile;
                sources[path] = lua;
                var target = await repository.SaveAsync(draft with { Revision = baseline.Revision, Sources = sources });
                var original = EditorSnapshotSerializer.Serialize(module).Content;
                var plan = SharedUiLayerSemanticAdoption.Review(module, target, [path]);
                Assert.Contains(plan.Workspace.Assignments, x => x.Slot.InputKey == "JOY_BTN1" && x.Command.Id == "NEW");
                Assert.Contains(plan.Workspace.Assignments, x => x.Slot.InputKey == "JOY_BTN2" && x.Command.Id == "CUSTOM");
                Assert.Equal(original, EditorSnapshotSerializer.Serialize(module).Content);
            }
            finally { File.Delete(file); }
        }

        /// <summary>Ambiguous name matches fail rather than selecting a shared physical device arbitrarily.</summary>
        [Fact]
        public async Task Rejects_ambiguous_fallback_without_changing_module()
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            try
            {
                var connection = "Data Source=" + file + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var baseline = await repository.SaveAsync(Defaults(false));
                var module = WithFallbackIdentity(Module(baseline), "Stick {33333333-3333-3333-3333-333333333333}.diff.lua");
                var draft = Defaults(true);
                var sources = new Dictionary<string, string>(draft.Sources);
                var lua = sources[ProfilePath];
                sources.Remove(ProfilePath);
                var first = Root + "input/UiLayer/joystick/Stick {11111111-1111-1111-1111-111111111111}.diff.lua";
                sources[first] = lua;
                sources[Root + "input/UiLayer/joystick/Stick {22222222-2222-2222-2222-222222222222}.diff.lua"] = lua;
                var target = await repository.SaveAsync(draft with { Revision = baseline.Revision, Sources = sources });
                var original = EditorSnapshotSerializer.Serialize(module).Content;
                var error = Assert.Throws<InvalidDataException>(() => SharedUiLayerSemanticAdoption.Review(module, target, [first]));
                Assert.Contains("Ambiguous shared default profile", error.Message, StringComparison.Ordinal);
                Assert.Equal(original, EditorSnapshotSerializer.Serialize(module).Content);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot WithFallbackIdentity(EditorWorkspaceSnapshot module, string file)
        {
            var state = WorkspaceManagement.Read(module)!;
            var sources = new Dictionary<string, string>(module.Sources!) {
                [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(state with {
                    Devices = state.Devices.Select(x => x.Layer == AssignmentLayer.UiLayer ? x with { FileName = file } : x).ToArray()
                })
            };
            return module with { Sources = sources,
                Diagrams = [module.Diagrams[0] with { CanonicalUiLayerSourcePath = null }] };
        }
        private static string Write(IReadOnlyList<DcsBinding> rows)
        {
            var id = rows[0].ProfileId;
            var module = new DcsModule(Guid.NewGuid(), "Test", "Test");
            var config = new ModuleConfiguration(module, [new(id, "stick", null, "stick", "Stick.diff.lua")], [], rows, "test");
            var axes = rows.Where(x => x.Section == BindingSection.Axis && x.Disposition != BindingDisposition.Removed).ToArray();
            var assignments = axes.Select(x => new DraftAssignment(new(module.Id, id, AssignmentLayer.Module, BindingSection.Axis, x.InputKey),
                new(x.CommandId, x.CommandName, true, AssignmentLayer.Module)) { Filter = x.Filter }).ToArray();
            var snapshot = new EditorWorkspaceSnapshot(config, [], [],
                [new("Stick", null, "stick.svg", 100, 100, [], []) { ProfileId = id,
                    Controls = axes.Select(x => new HardwareGraphicControl(x.InputKey, x.InputKey, x.InputKey, 0, 0, 10, 10, true)).ToArray() }],
                [], [], assignments, new Dictionary<string, string>());
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
        private static EditorWorkspaceSnapshot Module(SharedUiLayerWorkspace baseline, bool managed = true)
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
            var snapshot = new EditorWorkspaceSnapshot(new(module, [profile], [], [], "test"), [], [],
                [new("Stick", null, "stick.svg", 100, 100, [], []) { ProfileId = profile.Id, CanonicalUiLayerSourcePath = ProfilePath, Controls = controls }],
                [], [new(StableIdentity.Create("ui-function", "old"), catalog, "OLD", "Old", "UI", false),
                    new(StableIdentity.Create("ui-function", "new"), catalog, "NEW", "New", "UI", false)], assignments,
                new Dictionary<string, string> { [assignments[0].Slot.Key] = "my label" }, sources) { SharedDefault = baseline };
            snapshot = snapshot with { Assignments = snapshot.Assignments.Append(new DraftAssignment(
                new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN2", ""),
                new("AIRCRAFT", "Aircraft function", false, AssignmentLayer.Module))).ToArray() };
            if (managed) return snapshot;
            sources.Remove(WorkspaceManagement.SourcePath);
            sources.Remove("config/ipia-ui-" + profile.Id.ToString("N") + ".lua");
            return snapshot with { Diagrams = [snapshot.Diagrams[0] with {
                Callouts = controls.Select((x, i) => new UiLayerCallout(i + 1, x.Control, "Imported", "", x.X, x.Y, x.Width, x.Height)).ToArray()
            }] };

        }
        #endregion END private helpers
    }
}

