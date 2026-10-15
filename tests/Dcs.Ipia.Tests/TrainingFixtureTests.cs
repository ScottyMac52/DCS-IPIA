using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Validates the shipped training inputs using real import, SQLite and packaging boundaries.</summary>
    public sealed class TrainingFixtureTests
    {
        private const string FileName = "Logitech Throttle Quadrant {11111111-2222-3333-4444-555555555555}.diff.lua";
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
                var hardwareSources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                var uiPath = "assets/shared/ui-layer/input/UiLayer/joystick/" + FileName;
                var defaults = await new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(new(0, BundledSharedCatalog.SourceCommit(),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["assets/shared/ui-layer/functions.json"] = Asset("ui-commands.json"),
                        ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers",
                        [uiPath] = "local diff = {} return diff",
                        ["assets/shared/ui-layer/ipia-device-hardware.json"] = JsonSerializer.Serialize(new Dictionary<string, string> { [uiPath] = "logitech-throttle-quadrant" })
                    }));
                var workspace = NewModuleWorkspaceFactory.Create("TRAINING", "Training", defaults, hardwareSources);
                var hardware = WorkspaceStructureEditor.Hardware(workspace).Single(x => x.DeviceId == "logitech-throttle-quadrant");
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
                Assert.Equal(existing ? 4 : 3, workspace.ModuleCommands.Count);
                workspace = ModuleKneeboardPageEditor.GenerateSummary(ModuleKneeboardPageEditor.AddMissingDevicePages(workspace));
                await repository.SaveAsync(workspace);
                var saved = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(EditorSnapshotSerializer.Serialize(workspace).Sha256, EditorSnapshotSerializer.Serialize(saved).Sha256);
                Assert.Contains(saved.Pages, x => x.Kind == KneeboardPageKind.Summary);
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
