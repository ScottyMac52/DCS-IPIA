using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks selector data in workspaces saved before shifted controls were mapped.</summary>
    public sealed class LegacyEditorWorkspaceUpgradeTests
    {
        #region BEGIN Public
        /// <summary>Restores legacy shifted callouts, preserves edits, and does not resurrect later deletions.</summary>
        /// <returns>Completion of the SQLite round trip.</returns>
        [Fact]
        public async Task Legacy_saved_workspace_reopens_distinct_scope_and_modifier_assignments()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-legacy-" + Guid.NewGuid().ToString("N") + ".db");
            var connectionString = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
                var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
                var profile = new DeviceProfile(Guid.NewGuid(), "tm-mfd", null, "MFD", "MFD.diff.lua");
                var configuration = new ModuleConfiguration(
                    module, [profile], [new DcsModifier("Shift", "MFD", "JOY_BTN7", ModifierMode.Hold)], [], "import");
                await new SqliteConfigurationRepository(connectionString).ReplaceAsync(configuration);
                const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'>" +
                                   "<!-- box:osb-1 --><rect x='10' y='10' width='25' height='20'/>" +
                                   "<!-- box:osb-1-shifted --><rect x='40' y='10' width='25' height='20'/>" +
                                   "<text id='lbl-osb-1-shifted'>OSB 1 shifted</text></svg>";
                var diagram = new UiLayerDiagram("MFD", null, "assets/shared/hardware/svg/tm-mfd.svg", 100, 100, [],
                                                 [new UiLayerCallout(1, "osb-1-shifted", "UI shifted", "Shift", 40, 10,
                                                                     25, 20) { CommandId = "UI_SHIFT" }]) {
                    Svg = svg, ProfileId = profile.Id,
                    Controls = [new HardwareGraphicControl("osb-1", "OSB 1", "JOY_BTN1", 10, 10, 25, 20)]
                };
                var baseSlot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module,
                                                       BindingSection.Key, "JOY_BTN1");
                var shiftSlot = baseSlot with { ModifierChord = "Shift" };
                var baseCommand = new DraftCommand("BASE", "Module base", false, AssignmentLayer.Module);
                var shiftCommand = new DraftCommand("SHIFT", "Module shifted", false, AssignmentLayer.Module);
                var uiCommand = new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "UI_SHIFT", "UI shifted",
                                                         "General", false);
                var initial = new EditorWorkspaceSnapshot(
                    configuration, [], [], [diagram],
                    [
                        new CommandPaletteEntry("BASE", "Module base", "General", false),
                        new CommandPaletteEntry("SHIFT", "Module shifted", "General", false)
                    ],
                    [uiCommand],
                    [new DraftAssignment(baseSlot, baseCommand), new DraftAssignment(shiftSlot, shiftCommand)],
                    new Dictionary<string, string> { [baseSlot.Key] = "Custom base" });
                var repository = new SqliteEditorWorkspaceRepository(connectionString);
                await repository.SaveAsync(initial);
                await using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var read = connection.CreateCommand();
                    read.CommandText = "SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
                    read.Parameters.AddWithValue("$id", module.Id.ToString("D"));
                    var node = JsonNode.Parse((string)(await read.ExecuteScalarAsync())!)!.AsObject();
                    node.Remove("Version");
                    var write = connection.CreateCommand();
                    write.CommandText = "UPDATE editor_workspaces SET snapshot_json=$json WHERE module_id=$id";
                    write.Parameters.AddWithValue("$json", node.ToJsonString());
                    write.Parameters.AddWithValue("$id", module.Id.ToString("D"));
                    await write.ExecuteNonQueryAsync();
                }
                var restored = (await repository.GetAsync(module.Id))!;
                Assert.Equal(2, restored.Diagrams[0].Controls.Count);
                Assert.Contains(restored.Diagrams[0].Controls,
                                x => x.Control == "osb-1-shifted" && x.InputKey == "JOY_BTN1" && x.X == 40);
                Assert.Equal("Module base", restored.Assignments.Single(x => x.Slot.Key == baseSlot.Key).Command.Name);
                Assert.Equal("Module shifted",
                             restored.Assignments.Single(x => x.Slot.Key == shiftSlot.Key).Command.Name);
                var uiSlot = shiftSlot with { Layer = AssignmentLayer.UiLayer };
                Assert.Equal("UI shifted", restored.Assignments.Single(x => x.Slot.Key == uiSlot.Key).Command.Name);
                Assert.Equal("Custom base", restored.Labels[baseSlot.Key]);
                var draft = new DraftAssignmentService(
                    configuration,
                    [
                        baseCommand, shiftCommand,
                        new DraftCommand("UI_SHIFT", "UI shifted", false, AssignmentLayer.UiLayer)
                    ],
                    restored.Assignments);
                Assert.Equal(DraftOutcome.Applied, draft.Remove(uiSlot).Outcome);
                await repository.SaveAsync(restored with { Assignments = draft.Snapshot });
                var reopened = (await repository.GetAsync(module.Id))!;
                Assert.DoesNotContain(reopened.Assignments, x => x.Slot.Key == uiSlot.Key);
            }
            finally
            {
                File.Delete(path);
            }
        }
        /// <summary>Repairs a persisted Huey snapshot whose UI modifier is canonical and differs from the module modifier.</summary>
        /// <returns>Completion of the SQLite upgrade.</returns>
        [Fact]
        public async Task Empty_legacy_import_restores_both_scopes_from_stored_sources_once()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-empty-" + Guid.NewGuid().ToString("N") + ".db");
            var connectionString = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
                var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
                var profile = new DeviceProfile(Guid.NewGuid(), "tm-mfd", "MFD3", "MFD", "MFD3.diff.lua");
                var binding = new DcsBinding(profile.Id, BindingSection.Key, "MODULE_BASE", "Base action",
                                             BindingDisposition.Added, "JOY_BTN1");
                var configuration = new ModuleConfiguration(
                    module, [profile], [new DcsModifier("MOZA_F16_F18_BTN3", "MOZA", "JOY_BTN3", ModifierMode.Hold)],
                    [binding], "import");
                await new SqliteConfigurationRepository(connectionString).ReplaceAsync(configuration);
                var diagram = new UiLayerDiagram(
                    "MFD", "MFD3", "assets/shared/hardware/svg/tm-mfd.svg", 100, 100, [],
                    [new UiLayerCallout(1, "osb-1-shifted", "VR Zoom", "MOZA_MODIFIER_BTN3", 40, 10, 25, 20) {
                        CommandId = "UI_ZOOM"
                    }]) { Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'/>",
                          ProfileId = profile.Id, Controls = [
                              new("osb-1", "OSB 1", "JOY_BTN1", 10, 10, 25, 20),
                              new("osb-1-shifted", "OSB 1", "JOY_BTN1", 40, 10, 25, 20)
                          ] };
                var initial = new EditorWorkspaceSnapshot(
                    configuration, [], [], [diagram],
                    [new CommandPaletteEntry("MODULE_BASE", "Base action", "General", false)],
                    [new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "UI_ZOOM", "VR Zoom", "VR", false)], [],
                    new Dictionary<string, string>());
                var repository = new SqliteEditorWorkspaceRepository(connectionString);
                await repository.SaveAsync(initial);
                await using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var read = connection.CreateCommand();
                    read.CommandText = "SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
                    read.Parameters.AddWithValue("$id", module.Id.ToString("D"));
                    var payload = JsonNode.Parse((string)(await read.ExecuteScalarAsync())!)!.AsObject();
                    payload["Version"] = 1;
                    var write = connection.CreateCommand();
                    write.CommandText = "UPDATE editor_workspaces SET snapshot_json=$json WHERE module_id=$id";
                    write.Parameters.AddWithValue("$json", payload.ToJsonString());
                    write.Parameters.AddWithValue("$id", module.Id.ToString("D"));
                    await write.ExecuteNonQueryAsync();
                }
                var restored = (await repository.GetAsync(module.Id))!;
                Assert.Equal(2, restored.Assignments.Count);
                Assert.Contains(restored.Assignments, x => x.Slot.Layer == AssignmentLayer.Module &&
                                                           x.Slot.ModifierChord == "" && x.Command.Id == "MODULE_BASE");
                Assert.Contains(restored.Assignments, x => x.Slot.Layer == AssignmentLayer.UiLayer &&
                                                           x.Slot.ModifierChord == "MOZA_MODIFIER_BTN3" &&
                                                           x.Command.Id == "UI_ZOOM");
                var allowed =
                    restored.ModuleCommands
                        .Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                        .Concat(restored.UiCommands.Select(
                            x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer)));
                var draft =
                    new DraftAssignmentService(configuration, allowed, restored.Assignments, ["MOZA_MODIFIER_BTN3"]);
                Assert.Equal(
                    DraftOutcome.Applied,
                    draft.Remove(restored.Assignments.Single(x => x.Slot.Layer == AssignmentLayer.UiLayer).Slot)
                        .Outcome);
                await repository.SaveAsync(restored with { Assignments = draft.Snapshot });
                Assert.Single((await repository.GetAsync(module.Id))!.Assignments);
            }
            finally
            {
                File.Delete(path);
            }
        }
        /// <summary>Restores Hornet POV assignments omitted by the old hardware-key matcher without reviving later edits.</summary>
        /// <returns>Completion of the SQLite migration.</returns>
        [Fact]
        public async Task Older_snapshot_restores_only_previously_unmapped_pov_bindings()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-pov-" + Guid.NewGuid().ToString("N") + ".db");
            var connectionString = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
                var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
                var profile =
                    new DeviceProfile(Guid.NewGuid(), "moza-ab9-hornet-grip", null, "Hornet grip", "MOZA.diff.lua");
                var configuration = new ModuleConfiguration(
                    module, [profile], [new DcsModifier("SHIFT", "MOZA", "JOY_BTN3", ModifierMode.Hold)],
                    [
                        new DcsBinding(profile.Id, BindingSection.Key, "BASE", "Pilot Trimmer",
                                       BindingDisposition.Added, "JOY_BTN_POV1_U"),
                        new DcsBinding(profile.Id, BindingSection.Key, "SHIFTED", "Pilot Sight Elevation Increase",
                                       BindingDisposition.Added, "JOY_BTN_POV1_U", ["SHIFT"]),
                        new DcsBinding(profile.Id, BindingSection.Key, "OTHER", "Other", BindingDisposition.Added,
                                       "JOY_BTN11")
                    ],
                    "import");
                await new SqliteConfigurationRepository(connectionString).ReplaceAsync(configuration);
                var diagram =
                    new UiLayerDiagram("Hornet grip", null, "assets/shared/hardware/svg/grip-f18c.svg", 100, 100, [],
                                       []) { Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'/>",
                                             ProfileId = profile.Id, Controls = [
                                                 new("hornet-grip-trim-up", "Trim up", "JOY_POV1_U", 10, 10, 25, 20),
                                                 new("hornet-grip-vr-up", "VR up", "JOY_BTN11", 40, 10, 25, 20)
                                             ] };
                var otherSlot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module,
                                                        BindingSection.Key, "JOY_BTN11");
                var oldUiSlot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.UiLayer,
                                                        BindingSection.Key, "JOY_POV1_U");
                var initial = new EditorWorkspaceSnapshot(
                    configuration, [], [], [diagram],
                    [
                        new("BASE", "Pilot Trimmer", "Flight", false),
                        new("SHIFTED", "Pilot Sight Elevation Increase", "Flight", false),
                        new("OTHER", "Other", "Flight", false)
                    ],
                    [new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "UI", "UI action", "General", false)],
                    [
                        new(otherSlot, new("OTHER", "Other", false, AssignmentLayer.Module)),
                        new(oldUiSlot, new("UI", "UI action", false, AssignmentLayer.UiLayer))
                    ],
                    new Dictionary<string, string> { [oldUiSlot.Key] = "UI custom" });
                var repository = new SqliteEditorWorkspaceRepository(connectionString);
                await repository.SaveAsync(initial);
                await using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var read = connection.CreateCommand();
                    read.CommandText = "SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
                    read.Parameters.AddWithValue("$id", module.Id.ToString("D"));
                    var payload = JsonNode.Parse((string)(await read.ExecuteScalarAsync())!)!.AsObject();
                    payload["Version"] = 2;
                    var write = connection.CreateCommand();
                    write.CommandText = "UPDATE editor_workspaces SET snapshot_json=$json WHERE module_id=$id";
                    write.Parameters.AddWithValue("$json", payload.ToJsonString());
                    write.Parameters.AddWithValue("$id", module.Id.ToString("D"));
                    await write.ExecuteNonQueryAsync();
                }
                var restored = (await repository.GetAsync(module.Id))!;
                var baseSlot = otherSlot with { InputKey = "JOY_BTN_POV1_U" };
                var shiftedSlot = baseSlot with { ModifierChord = "SHIFT" };
                var uiSlot = oldUiSlot with { InputKey = "JOY_BTN_POV1_U" };
                Assert.Equal("BASE", restored.Assignments.Single(x => x.Slot.Key == baseSlot.Key).Command.Id);
                Assert.Equal("SHIFTED", restored.Assignments.Single(x => x.Slot.Key == shiftedSlot.Key).Command.Id);
                Assert.Equal("UI", restored.Assignments.Single(x => x.Slot.Key == uiSlot.Key).Command.Id);
                Assert.Equal("UI custom", restored.Labels[uiSlot.Key]);
                Assert.DoesNotContain(restored.Assignments, x => x.Slot.Key == oldUiSlot.Key);
                await repository.SaveAsync(
                    restored with { Assignments =
                                        restored.Assignments.Where(x => x.Slot.Key != baseSlot.Key).ToArray() });
                Assert.DoesNotContain((await repository.GetAsync(module.Id))!.Assignments,
                                      x => x.Slot.Key == baseSlot.Key);
            }
            finally
            {
                File.Delete(path);
            }
        }
        #endregion END Public
    }
}
