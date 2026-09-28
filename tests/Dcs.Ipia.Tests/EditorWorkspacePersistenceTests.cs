using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks local persistence of graphical editor data and saved assignments.</summary>
    public sealed class EditorWorkspacePersistenceTests
    {
        #region BEGIN Public
        /// <summary>Reopens the actual SVG, named categories, and changed callout assignment without source files.</summary>
        /// <returns>Completion of the database round trip.</returns>
        [Fact]
        public async Task Editor_reopens_saved_svg_catalog_assignment_and_label()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-workspace-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
                var profile = new DeviceProfile(Guid.NewGuid(), "tm-mfd", null, "MFD", "MFD.diff.lua");
                var configuration = new ModuleConfiguration(module, [profile], [], [], "import");
                await new SqliteConfigurationRepository(connection).ReplaceAsync(configuration);
                const string svg =
                    "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100'/></svg>";
                var diagram = new UiLayerDiagram(
                    "MFD", null, "assets/shared/hardware/svg/tm-mfd.svg", 100, 100, [],
                    []) { Svg = svg, ProfileId = profile.Id,
                          Controls = [new HardwareGraphicControl("osb-1", "OSB1", "JOY_BTN1", 10, 10, 25, 20)] };
                var command = new CommandPaletteEntry("FIRE", "Fire", "Weapons", false);
                var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key,
                                                   "JOY_BTN1");
                var initial = new EditorWorkspaceSnapshot(configuration, [], [], [diagram], [command], [], [],
                                                          new Dictionary<string, string>());
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(initial);
                var draft = new DraftAssignmentService(
                    configuration, [new DraftCommand(command.Id, command.Name, false, AssignmentLayer.Module)], []);
                Assert.Equal(DraftOutcome.Applied,
                             draft
                                 .Assign(slot,
                                         new DraftCommand(command.Id, command.Name, false, AssignmentLayer.Module),
                                         OccupiedDestinationBehavior.Ask)
                                 .Outcome);
                await repository.SaveAsync(
                    initial with { Assignments = draft.Snapshot,
                                   Labels = new Dictionary<string, string> { [slot.Key] = "Trigger" } });
                var restored = await repository.GetAsync(module.Id);
                Assert.NotNull(restored);
                Assert.Equal(svg, restored.Diagrams[0].Svg);
                Assert.Equal("Weapons", restored.ModuleCommands[0].Category);
                Assert.Equal("FIRE", Assert.Single(restored.Assignments).Command.Id);
                Assert.Equal("Trigger", restored.Labels[slot.Key]);
                var secondModule = new DcsModule(Guid.NewGuid(), "F-14BU", "Tomcat");
                var secondConfig = new ModuleConfiguration(secondModule, [], [], [], "import");
                await new SqliteConfigurationRepository(connection).ReplaceAsync(secondConfig);
                await repository.SaveAsync(
                    new EditorWorkspaceSnapshot(secondConfig, [], [], [], [], [], [], new Dictionary<string, string>(),
                                                new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" }));
                Assert.Equal(2, (await repository.ListAsync()).Count);
                var second = await repository.GetAsync(secondModule.Id);
                Assert.Equal("F-14BU", second!.Configuration.Module.DcsId);
                Assert.Equal("{}", second.Sources!["config/kneeboard.json"]);
                Assert.Equal("Trigger", (await repository.GetAsync(module.Id))!.Labels[slot.Key]);
                var axisSlot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module,
                                                       BindingSection.Axis, "JOY_X");
                var axisCommand = new DraftCommand("PITCH", "Pitch", true, AssignmentLayer.Module);
                var filter = new AxisFilter(0.04, 1, 0.85, [0.2, -0.2], true, false, true, 0.9, 1);
                var current = (await repository.GetAsync(module.Id))!;
                await repository.SaveAsync(current with {
                    ModuleCommands =
                        [..current.ModuleCommands, new CommandPaletteEntry("PITCH", "Pitch", "Axes", true)],
                    Assignments =
                        [..current.Assignments, new DraftAssignment(axisSlot, axisCommand) { Filter = filter }]
                });
                var restoredAxis = (await repository.GetAsync(module.Id))!.Assignments.Single(x => x.Slot == axisSlot);
                Assert.Equal(filter.Deadzone, restoredAxis.Filter!.Deadzone);
                Assert.Equal(filter.Curvature, restoredAxis.Filter.Curvature);
                Assert.Equal(filter.HardwareDetentAB, restoredAxis.Filter.HardwareDetentAB);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>A frozen workspace restores into an empty database without source files or a partial write.</summary>
        /// <returns>Completion of the SQLite restore and rollback checks.</returns>
        [Fact]
        public async Task Frozen_workspace_restores_into_empty_database_atomically()
        {
            var sourcePath = Path.Combine(Path.GetTempPath(), $"ipia-source-{Guid.NewGuid():N}.db");
            var targetPath = Path.Combine(Path.GetTempPath(), $"ipia-restored-{Guid.NewGuid():N}.db");
            try
            {
                var sourceConnection = "Data Source=" + sourcePath + ";Pooling=False";
                var targetConnection = "Data Source=" + targetPath + ";Pooling=False";
                await new SqliteMigrationRunner(sourceConnection).MigrateAsync();
                await new SqliteMigrationRunner(targetConnection).MigrateAsync();

                var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
                var profile = new DeviceProfile(Guid.NewGuid(), "tm-mfd", "MFD1", "pilot-left", "MFD.diff.lua");
                var configuration = new ModuleConfiguration(module, [profile], [], [], "pinned-source");
                var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module,
                                                   BindingSection.Axis, "JOY_X");
                var filter = new AxisFilter(0.04, 1, 0.85, [0.2, -0.2], true, false, true, 0.9, 1);
                var workspace = new EditorWorkspaceSnapshot(
                    configuration, [], [], [], [new CommandPaletteEntry("PITCH", "Pitch", "Axes", true)], [],
                    [new DraftAssignment(slot, new DraftCommand("PITCH", "Pitch", true, AssignmentLayer.Module)) {
                        Filter = filter
                    }], new Dictionary<string, string> { [slot.Key] = "Cyclic pitch" },
                    new Dictionary<string, string> { ["config/kneeboard.json"] = "{\"schemaVersion\":1}" });
                await new SqliteEditorWorkspaceRepository(sourceConnection).SaveAsync(workspace);
                var frozen = EditorSnapshotSerializer.Serialize(
                    (await new SqliteEditorWorkspaceRepository(sourceConnection).GetAsync(module.Id))!);

                var restored = EditorSnapshotSerializer.Deserialize(frozen);
                var target = new SqliteEditorWorkspaceRepository(targetConnection);
                await target.SaveAsync(restored);
                var reopened = (await target.GetAsync(module.Id))!;
                Assert.Equal(frozen, EditorSnapshotSerializer.Serialize(reopened));
                Assert.Equal(filter, Assert.Single(reopened.Assignments).Filter);
                Assert.Equal("Cyclic pitch", reopened.Labels[slot.Key]);
                Assert.Equal("{\"schemaVersion\":1}", reopened.Sources!["config/kneeboard.json"]);

                var collision = new ModuleConfiguration(new(Guid.NewGuid(), "UH-1H", "Duplicate"), [], [], [],
                                                        "another-source");
                await Assert.ThrowsAsync<SqliteException>(() => target.SaveAsync(
                    workspace with { Configuration = collision, Assignments = [], Labels = new Dictionary<string, string>() }));
                Assert.Equal(module.Id, Assert.Single(await target.ListAsync()));
                Assert.Equal(frozen, EditorSnapshotSerializer.Serialize((await target.GetAsync(module.Id))!));
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(targetPath);
            }
        }
        #endregion END Public
    }
}
