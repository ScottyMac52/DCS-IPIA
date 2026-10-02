using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Packaging;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises source-free authoring, verified restore and snapshot-only package reproducibility.</summary>
    public sealed class ModuleSnapshotWorkflowTests
    {
        #region BEGIN Public
        /// <summary>Authored hardware, commands, both assignment scopes and tuned axes survive an empty-database restore.</summary>
        /// <returns>Completion of SQLite and independent archive checks.</returns>
        [Fact]
        public async Task New_module_restores_and_rebuilds_identical_output_without_live_sources()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-roundtrip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var original = await WorkspaceAsync();
                var connection = "Data Source=" + Path.Combine(root, "original.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.CreateAsync(original);
                var saved = (await repository.GetAsync(original.Configuration.Module.Id))!;
                var snapshot = ModuleSnapshotWorkflow.Freeze(saved, "2.3.4");
                var first = ModuleSnapshotWorkflow.Build(snapshot);
                Assert.Equal(first.Sha256, OvGmePackageVerifier.Verify(first));
                var file = Path.Combine(root, "aircraft.ipia-module.json");
                ModuleSnapshotWorkflow.Write(snapshot, file);
                var read = ModuleSnapshotWorkflow.Read(File.ReadAllText(file));
                Assert.Equal(snapshot, read);
                Assert.Throws<IOException>(() => ModuleSnapshotWorkflow.Write(snapshot, file));
                Assert.Empty(Directory.GetFiles(root, "*.tmp"));

                var target = "Data Source=" + Path.Combine(root, "empty.db") + ";Pooling=False";
                await new SqliteMigrationRunner(target).MigrateAsync();
                var restoredRepository = new SqliteEditorWorkspaceRepository(target);
                var defaultsRepository = new SqliteSharedUiLayerWorkspaceRepository(target);
                Assert.Null(await defaultsRepository.GetAsync());
                await ModuleSnapshotWorkflow.RestoreAsync(read, restoredRepository);
                var reopened = (await restoredRepository.GetAsync(original.Configuration.Module.Id))!;
                Assert.Equal(snapshot.Workspace, EditorSnapshotSerializer.Serialize(reopened));
                Assert.Equal(original.SharedDefault!.ContentSha256, reopened.SharedDefault!.ContentSha256);
                Assert.Null(await defaultsRepository.GetAsync());
                Assert.Equal("Pitch custom", reopened.Labels.Values.Single());
                Assert.Equal(4, reopened.Assignments.Count);
                var tuned = reopened.Assignments.Single(x => x.Command.Id == "a2001cdnil");
                Assert.Equal(3d, tuned.Filter!.Deadzone);
                Assert.Equal(new double[] { 12 }, tuned.Filter.Curvature);
                Assert.Equal("SHIFT", tuned.Slot.ModifierChord);
                Assert.Contains(WorkspaceStructureEditor.Describe(reopened).Modifiers, x => x.Name == "SHIFT" && x.Mode == ModifierMode.Toggle);
                var second = ModuleSnapshotWorkflow.Build(ModuleSnapshotWorkflow.Freeze(reopened, "2.3.4"));
                Assert.Equal(first.Bytes, second.Bytes);
                Assert.Equal(first.ChecksumLine, second.ChecksumLine);
                var changed = reopened with { Labels = reopened.Labels.ToDictionary(x => x.Key, _ => "Changed label") };
                Assert.NotEqual(first.Bytes, ModuleSnapshotWorkflow.Build(ModuleSnapshotWorkflow.Freeze(changed, "2.3.4")).Bytes);
                Assert.Equal(first.Bytes, ModuleSnapshotWorkflow.Build(read).Bytes);
                using var stream = new MemoryStream(first.Bytes);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                Assert.DoesNotContain(zip.Entries, x => x.FullName.Contains(".db", StringComparison.Ordinal) || x.FullName.Contains("ipia-", StringComparison.Ordinal));
                Assert.Equal(3, zip.Entries.Count(x => x.FullName.EndsWith(".png", StringComparison.Ordinal)));
                var axisProfile = reopened.Configuration.Profiles.Single(x => x.DeviceId == "moza-ab9");
                var axisPath = "DCS-ROUNDTRIP-Components-2.3.4-OVGME/Config/Input/ROUNDTRIP/joystick/" + axisProfile.SourceName;
                var axis = DcsLuaParser.ParseProfile(Text(zip, axisPath), axisProfile.Id).Single(x => x.CommandId == "a2001cdnil");
                Assert.Equal(3d, axis.Filter!.Deadzone);
                Assert.Equal("SHIFT", Assert.Single(axis.Reformers));
                Assert.Equal(first.Bytes, File.ReadAllBytes(OvGmeArtifactWriter.Write(second, root).ArchivePath));
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>Duplicate module IDs and case-insensitive DCS identities cannot overwrite modules or shared revisions.</summary>
        /// <returns>Completion of insert-only storage checks.</returns>
        [Fact]
        public async Task Restore_rejects_duplicates_and_preserves_other_modules_and_defaults()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-restore-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var workspace = await WorkspaceAsync();
                var shared = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var defaults = await shared.SaveAsync(workspace.SharedDefault! with { Revision = 0 });
                var other = workspace with { Configuration = new(new(Guid.NewGuid(), "OTHER", "Other module"), [], [], [], "other"),
                    Pages = [], Assignments = [], Diagrams = [] };
                await repository.CreateAsync(other);
                var otherBefore = EditorSnapshotSerializer.Serialize((await repository.GetAsync(other.Configuration.Module.Id))!);
                var snapshot = ModuleSnapshotWorkflow.Freeze(workspace, "1");
                await ModuleSnapshotWorkflow.RestoreAsync(snapshot, repository);
                await Assert.ThrowsAsync<InvalidOperationException>(() => ModuleSnapshotWorkflow.RestoreAsync(snapshot, repository));
                var collision = workspace with { Configuration = new(new(Guid.NewGuid(), "roundtrip", "Collision"), [], [], [], "collision"), Pages = [], Assignments = [] };
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(collision));
                Assert.Equal(2, (await repository.ListAsync()).Count);
                Assert.Equal(snapshot.Workspace, EditorSnapshotSerializer.Serialize((await repository.GetAsync(workspace.Configuration.Module.Id))!));
                Assert.Equal(otherBefore, EditorSnapshotSerializer.Serialize((await repository.GetAsync(other.Configuration.Module.Id))!));
                Assert.Equal(defaults.ContentSha256, (await shared.GetAsync())!.ContentSha256);
                await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(workspace with {
                    Configuration = new(new(Guid.Empty, "INVALID", "Invalid"), [], [], [], "invalid") }));
            }
            finally { File.Delete(path); }
        }

        /// <summary>Corrupt content and incomplete commands, hardware, pages or assets fail before restore or artifact writes.</summary>
        /// <returns>Completion of failure-path checks.</returns>
        [Fact]
        public async Task Invalid_snapshots_do_not_write_modules_or_artifacts()
        {
            var workspace = await WorkspaceAsync();
            var snapshot = ModuleSnapshotWorkflow.Freeze(workspace, "1");
            var corrupt = snapshot with { Workspace = snapshot.Workspace with { Sha256 = new string('0', 64) } };
            Assert.Throws<InvalidDataException>(() => ModuleSnapshotWorkflow.Build(corrupt));
            Assert.Throws<JsonException>(() => ModuleSnapshotWorkflow.Read("{"));
            Assert.Throws<InvalidDataException>(() => ModuleSnapshotWorkflow.Read("null"));
            Assert.Throws<JsonException>(() => ModuleSnapshotWorkflow.Read("{\"unsupported\":true}"));
            Assert.Throws<ArgumentException>(() => ModuleSnapshotWorkflow.Freeze(workspace, "../1"));
            Assert.Throws<ArgumentException>(() => ModuleSnapshotWorkflow.Freeze(workspace with { ModuleCommands = [] }, "1"));
            Assert.Throws<InvalidDataException>(() => ModuleSnapshotWorkflow.Freeze(workspace with { Pages = [] }, "1"));
            var sources = workspace.Sources!.Where(x => x.Key != "config/ipia-physical-bindings.json").ToDictionary(x => x.Key, x => x.Value);
            Assert.Throws<InvalidDataException>(() => ModuleSnapshotWorkflow.Freeze(workspace with { Sources = sources }, "1"));
            Assert.Throws<ArgumentException>(() => ModuleSnapshotWorkflow.Freeze(workspace with {
                Images = [new("bad", "bad.png", [1], "bad")] }, "1"));
            var path = Path.Combine(Path.GetTempPath(), "ipia-invalid-" + Guid.NewGuid().ToString("N") + ".db");
            var artifact = path + ".json";
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await Assert.ThrowsAsync<InvalidDataException>(() => ModuleSnapshotWorkflow.RestoreAsync(corrupt, repository));
                Assert.Empty(await repository.ListAsync());
                Assert.Throws<InvalidDataException>(() => ModuleSnapshotWorkflow.Write(corrupt, artifact));
                Assert.False(File.Exists(artifact));
            }
            finally { File.Delete(path); File.Delete(artifact); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<EditorWorkspaceSnapshot> WorkspaceAsync()
        {
            var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal) {
                ["assets/shared/ui-layer/functions.json"] = """{"schemaVersion":1,"functions":[{"id":"pause","command":"d3001pnilu3001cdnilvd1vpnilvunil","label":"Pause","category":"General"}]}""",
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
            };
            var commit = new string('a', 40);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit + "\n" + JsonSerializer.Serialize(sources))));
            var defaults = new SharedUiLayerWorkspace(1, commit, sources, hash);
            var workspace = NewModuleWorkspaceFactory.Create("ROUNDTRIP", "Authored aircraft", defaults, hardware);
            var choices = WorkspaceStructureEditor.Hardware(workspace);
            foreach (var id in new[] { "tm-mfd", "moza-ab9" })
            {
                var choice = choices.Single(x => x.DeviceId == id);
                workspace = NewModuleDeviceAuthoring.Apply(workspace, new(choice,
                    new DetectedDcsDevice(Guid.NewGuid(), choice.Label, false).FileName, id, "Pilot"));
            }
            var mfd = workspace.Configuration.Profiles.Single(x => x.DeviceId == "tm-mfd");
            var stick = workspace.Configuration.Profiles.Single(x => x.DeviceId == "moza-ab9");
            workspace = WorkspaceStructureEditor.AddDevice(workspace, choices.Single(x => x.DeviceId == "tm-mfd"), AssignmentLayer.UiLayer, mfd.SourceName, "tm-mfd", "Pilot");
            var button = workspace.Diagrams.Single(x => x.ProfileId == mfd.Id).Controls.First(x => !x.IsAxis && x.InputKey == "JOY_BTN1");
            workspace = WorkspaceStructureEditor.SetModifier(workspace, null, new(AssignmentLayer.Module, mfd.Id, "SHIFT", "JOY_BTN2", "Shifted", ModifierMode.Toggle, ""));
            workspace = ModuleCommandCatalogImport.Apply(workspace, ModuleCommandCatalogImport.Review(workspace,
                [new("commands.json", """{"schemaVersion":1,"moduleId":"ROUNDTRIP","commands":[{"bindingKey":"d3002pnilu3002cdnilvd1vpnilvunil","name":"Aircraft action","categoryPath":["Systems"],"type":"button"},{"bindingKey":"a2001cdnil","name":"Pitch","categoryPath":["Flight"],"type":"axis"}]}""")], false));
            var key = new DraftCommand(workspace.ModuleCommands.Single(x => !x.IsAxis).Id, "Aircraft action", false, AssignmentLayer.Module);
            var axis = new DraftCommand("a2001cdnil", "Pitch", true, AssignmentLayer.Module);
            var ui = new DraftCommand(workspace.UiCommands.Single().DcsCommandId, "Pause", false, AssignmentLayer.UiLayer);
            var module = workspace.Configuration.Module.Id;
            var axisControl = workspace.Diagrams.Single(x => x.ProfileId == stick.Id).Controls.First(x => x.IsAxis);
            var axisSlot = new DraftAssignmentSlot(module, stick.Id, AssignmentLayer.Module, BindingSection.Axis, axisControl.InputKey, "SHIFT");
            workspace = workspace with {
                Assignments = [new(new(module, mfd.Id, AssignmentLayer.Module, BindingSection.Key, button.InputKey), key),
                    new(new(module, mfd.Id, AssignmentLayer.Module, BindingSection.Key, button.InputKey, "SHIFT"), key),
                    new(new(module, mfd.Id, AssignmentLayer.UiLayer, BindingSection.Key, button.InputKey), ui),
                    new(axisSlot, axis) { Filter = new(Deadzone: 3, Curvature: [12], Invert: true, Slider: false) }],
                Labels = new Dictionary<string, string> { [axisSlot.Key] = "Pitch custom" }
            };
            workspace = ModuleKneeboardPageEditor.AddMissingDevicePages(workspace);
            return ModuleKneeboardPageEditor.Set(workspace, null, new(KneeboardPageKind.Summary, null, "00-OVERVIEW", "OVERVIEW", "CONTROLS", 0,
                [new("MFD", "Aircraft and UI actions", "cyan"), new("STICK", "Flight controls", "gold")]));
        }
        private static string Text(ZipArchive zip, string path)
        {
            using var reader = new StreamReader(zip.GetEntry(path)!.Open());
            return reader.ReadToEnd();
        }
        #endregion END private helpers
    }
}
