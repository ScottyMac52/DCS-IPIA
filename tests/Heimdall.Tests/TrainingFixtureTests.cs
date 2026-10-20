using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Validates the shipped training inputs using real import, SQLite and packaging boundaries.</summary>
    public sealed class TrainingFixtureTests
    {
        private const string FileName = "Generic training joystick {11111111-2222-3333-4444-555555555555}.diff.lua";
        #region BEGIN Public
        /// <summary>Both bundled starting points retain expected state and generate valid portable package inputs.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Bundled_configuration_imports_saves_reopens_and_builds(bool existing)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-training-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "joystick"));
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var sample = JsonSerializer.Deserialize<WorkspaceHardware>(Asset("joystick.json"))!;
                sample = sample with { Diagram = sample.Diagram with { Svg = Asset("joystick.svg") } };
                var hardwareSources = new Dictionary<string, string> { ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { sample }),
                    [sample.Diagram.Path] = sample.Diagram.Svg };
                var uiPath = "assets/shared/ui-layer/input/UiLayer/joystick/" + FileName;
                var defaults = await new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(new(0, BundledSharedCatalog.SourceCommit(),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["assets/shared/ui-layer/functions.json"] = Asset("ui-commands.json"),
                        ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers",
                        [uiPath] = "local diff = {} return diff",
                        ["assets/shared/ui-layer/ipia-device-hardware.json"] = JsonSerializer.Serialize(new Dictionary<string, string> { [uiPath] = "training-joystick" })
                    }));
                var workspace = NewModuleWorkspaceFactory.Create("TRAINING", "Training", defaults, hardwareSources);
                var hardware = WorkspaceStructureEditor.Hardware(workspace).Single(x => x.DeviceId == "training-joystick");
                if (existing)
                {
                    await File.WriteAllTextAsync(Path.Combine(root, "joystick", FileName), Asset("profile.lua"));
                    await File.WriteAllTextAsync(Path.Combine(root, "modifiers.lua"), Asset("modifiers.lua"));
                    var folder = await new DcsFolderImporter().ImportAsync(root);
                    Assert.True(folder.Succeeded, string.Join(";", folder.Diagnostics.Select(x => x.Message)));
                    Assert.Contains(folder.Bindings, x => x.Disposition == BindingDisposition.Removed);
                    workspace = SavedGamesWorkspaceFactory.Create(folder, "TRAINING", "Training", defaults, hardwareSources,
                        [new(folder.Profiles.Single().Id, hardware)]);
                    Assert.Contains(workspace.Assignments, x => x.Filter?.Deadzone == 2);
                    Assert.Contains(workspace.Assignments, x => x.Slot.ModifierChord == "TRAIN_SHIFT");
                    Assert.Equal(Asset("profile.lua"), await File.ReadAllTextAsync(Path.Combine(root, "joystick", FileName)));
                }
                else
                {
                    workspace = NewModuleDeviceAuthoring.Apply(workspace, new(hardware, FileName, null, null, FileName));
                }
                var catalog = Path.Combine(root, "commands.json");
                await File.WriteAllTextAsync(catalog, Asset("commands.json"));
                workspace = ModuleCommandCatalogImport.Apply(workspace, ModuleCommandCatalogImport.ReadFiles(workspace, [catalog], false));
                Assert.Equal(10, workspace.ModuleCommands.Count);
                var device = WorkspaceStructureEditor.Describe(workspace).Devices.Single(x => x.Layer == AssignmentLayer.Module);
                if (!existing)
                    workspace = WorkspaceStructureEditor.SetModifier(workspace, null,
                        new(AssignmentLayer.Module, device.ProfileId, "TRAIN_SHIFT", "JOY_BTN3", "TRAIN_SHIFT", ModifierMode.Hold, ""));
                workspace = WorkspaceStructureEditor.SetModifier(workspace, null,
                    new(AssignmentLayer.Module, device.ProfileId, "TRAIN_TOGGLE", "JOY_BTN4", "TRAIN_TOGGLE", ModifierMode.Toggle, ""));
                var draft = new DraftAssignmentService(workspace.Configuration,
                    workspace.ModuleCommands.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                        .Concat(workspace.UiCommands.Select(x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer))),
                    workspace.Assignments, WorkspaceStructureEditor.UiModifierNames(workspace));
                var module = workspace.Configuration.Module.Id;
                var light = new DraftAssignmentSlot(module, device.ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
                var shifted = light with { ModifierChord = "TRAIN_SHIFT" };
                var axis = light with { Section = BindingSection.Axis, InputKey = "JOY_Y" };
                var uiDevice = WorkspaceStructureEditor.Describe(workspace).Devices.Single(x => x.Layer == AssignmentLayer.UiLayer);
                var ui = light with { Layer = AssignmentLayer.UiLayer, ProfileId = uiDevice.ProfileId, InputKey = "JOY_BTN2" };
                Assert.Equal(DraftOutcome.Applied, draft.Assign(light, new("d3001pnilu3001cd1vd1vpnilvu0", "Light", false, AssignmentLayer.Module), OccupiedDestinationBehavior.Reject).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Assign(shifted, new("d3002pnilu3002cd1vd1vpnilvu0", "Shifted", false, AssignmentLayer.Module), OccupiedDestinationBehavior.Reject).Outcome);
                Assert.Equal(existing ? DraftOutcome.NoChange : DraftOutcome.Applied, draft.Assign(axis, new("a2001cdnil", "Sample pitch axis", true, AssignmentLayer.Module), OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.TuneAxis(axis, new(Deadzone: 3, Curvature: new double[] { 15 })).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Assign(ui, new("d52pnilunilcdnilvdnilvpnilvunil", "Pause", false, AssignmentLayer.UiLayer), OccupiedDestinationBehavior.Reject).Outcome);
                var roll = axis with { InputKey = "JOY_X" };
                Assert.Equal(DraftOutcome.Applied, draft.Assign(roll, new("a2002cdnil", "Sample roll axis", true, AssignmentLayer.Module), OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Assign(light with { ModifierChord = "TRAIN_TOGGLE" },
                    new("d3003pnilu3003cd1vd1vpnilvu0", "Sample toggle-layer action", false, AssignmentLayer.Module), OccupiedDestinationBehavior.Reject).Outcome);
                for (var index = 0; index < 5; index++)
                {
                    var command = workspace.ModuleCommands.Single(x => x.Id == $"d{3004 + index}pnilu{3004 + index}cd1vd1vpnilvu0");
                    Assert.Equal(DraftOutcome.Applied, draft.Assign(light with { InputKey = "JOY_BTN" + (6 + index) },
                        new(command.Id, command.Name, false, AssignmentLayer.Module), OccupiedDestinationBehavior.Reject).Outcome);
                }
                workspace = workspace with { Assignments = draft.Snapshot, Labels = new Dictionary<string, string> { [light.Key] = "LIGHTS" } };
                workspace = ModuleKneeboardPageEditor.GenerateSummary(ModuleKneeboardPageEditor.AddMissingDevicePages(workspace));
                await repository.SaveAsync(workspace);
                var saved = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(EditorSnapshotSerializer.Serialize(workspace).Sha256, EditorSnapshotSerializer.Serialize(saved).Sha256);
                Assert.Contains(saved.Pages, x => x.Kind == KneeboardPageKind.Summary);
                Assert.Contains(WorkspaceStructureEditor.Describe(saved).Modifiers, x => x.Name == "TRAIN_TOGGLE" && x.Mode == ModifierMode.Toggle);
                var plan = GeneratedInputTestPlan.Create(saved);
                var session = new InputTestSession(plan.Bindings, plan.Modifiers);
                var physical = Guid.Parse("11111111-2222-3333-4444-555555555555");
                var values = Enumerable.Range(1, 10).ToDictionary(x => "JOY_BTN" + x, _ => 0.0);
                values["JOY_X"] = values["JOY_Y"] = 0.5;
                IReadOnlyList<InputTestResult> Sample() => session.ProcessDevice([new(physical, new Dictionary<string, double>(values))], physical);
                Assert.Empty(Sample());
                values["JOY_BTN4"] = 1;
                Sample();
                values["JOY_BTN4"] = 0;
                values["JOY_BTN1"] = 1;
                Assert.Contains(Sample(), x => x.Matches.Any(match => match.Chord == "TRAIN_TOGGLE"));
                values["JOY_BTN4"] = 1;
                Sample();
                values["JOY_BTN4"] = values["JOY_BTN1"] = 0;
                Sample();
                values["JOY_BTN3"] = values["JOY_BTN1"] = 1;
                Assert.Contains(Sample(), x => x.Matches.Any(match => match.Chord == "TRAIN_SHIFT"));
                values["JOY_BTN3"] = values["JOY_BTN1"] = 0;
                Sample();
                values["JOY_X"] = 0.75;
                values["JOY_Y"] = 0.25;
                var moved = Sample();
                Assert.Contains(moved, x => x.InputKey == "JOY_Y" && x.Matches.Any(match => match.Binding.Filter?.Deadzone == 3));
                Assert.Contains(moved, x => x.InputKey == "JOY_X" && x.Matches.Any(match => match.Binding.Filter is null));
                var snapshot = ModuleSnapshotWorkflow.Freeze(saved, "1.0.0");
                Assert.NotEmpty(ModuleSnapshotWorkflow.Export(snapshot));
                Assert.NotNull(ModuleSnapshotWorkflow.Build(snapshot));
                Assert.Equal(defaults.ContentSha256, (await new SqliteSharedUiLayerWorkspaceRepository(connection).GetAsync())!.ContentSha256);
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Asset(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Training", file));
        #endregion END private helpers
    }
}
