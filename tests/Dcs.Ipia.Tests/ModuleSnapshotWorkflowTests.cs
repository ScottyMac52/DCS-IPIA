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
        /// <summary>The Windows authoring smoke fixture can explicitly acquire a UI Layer profile before Fly Now.</summary>
        [Fact]
        public void Smoke_module_with_explicit_ui_membership_builds_both_scopes()
        {
            var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", "Test.diff.lua");
            var module = new DcsModule(Guid.NewGuid(), "AUTHORING", "Authoring");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "Created in IPIA");
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
            {
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100'/></svg>",
                Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20)]
            };
            var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [diagram], [], [], [], new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    ["config/kneeboard.json"] = """{"profiles":{},"pages":[],"summaryPages":[]}""",
                    ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
                });
            workspace = WorkspaceStructureEditor.AddDevice(workspace, WorkspaceStructureEditor.Hardware(workspace).Single(),
                AssignmentLayer.UiLayer, profile.SourceName, profile.Instance, null);
            const string pause = "d3001pnilu3001cdnilvd1vpnilvunil";
            workspace = workspace with
            {
                UiCommands = [new(Guid.NewGuid(), module.Id, pause, "Pause", "General", false)],
                Assignments = [new(new(module.Id, profile.Id, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"),
                    new(pause, "Pause", false, AssignmentLayer.UiLayer))]
            };
            workspace = ModuleKneeboardPageEditor.AddMissingDevicePages(workspace);
            workspace = ModuleKneeboardPageEditor.Set(workspace, null, new(KneeboardPageKind.Summary, null, "00-OVERVIEW", "OVERVIEW", "CONTROLS", 0,
                [new("TEST", "Aircraft and UI", "cyan")]));
            var package = ModuleSnapshotWorkflow.Build(new ModuleBuildSnapshot("FlyNow", EditorSnapshotSerializer.Serialize(workspace)));
            using var zip = new ZipArchive(new MemoryStream(package.Bytes));
            var root = package.FileName[..^4] + "/Config/Input/";
            Assert.NotNull(zip.GetEntry(root + "AUTHORING/joystick/Test.diff.lua"));
            Assert.Contains(pause, Text(zip, root + "UiLayer/joystick/Test.diff.lua"), StringComparison.Ordinal);
        }

        /// <summary>Fly Now deploys the verified package payload and Clear leaves authoring data intact.</summary>
        [Fact]
        public async Task Fly_now_uses_the_exact_saved_module_and_ui_layer_payload()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-fly-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var workspace = await WorkspaceAsync();
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var builder = new FlyNowPayloadBuilder(repository);
                var module = workspace.Configuration.Module.Id;
                var expected = await builder.BuildAsync(module);
                Assert.Contains(expected.Files.Keys, x => x.StartsWith("Config/Input/UiLayer/", StringComparison.Ordinal));
                Assert.Contains(expected.Files.Keys, x => x.StartsWith("KNEEBOARD/", StringComparison.Ordinal));
                Assert.DoesNotContain(expected.Files.Keys, x => x.Contains("README", StringComparison.Ordinal));
                var destination = Path.Combine(root, "DCS");
                Directory.CreateDirectory(destination);
                var service = new SqliteFlyNowService(connection, Path.Combine(root, "backups"), builder);
                var deployed = await service.DeployAsync(module, destination);
                Assert.Equal(expected.Files.Keys.Order(StringComparer.Ordinal), deployed.Files);
                foreach (var file in expected.Files)
                    Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(destination, file.Key)));
                await service.ClearAsync();
                Assert.Empty(Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
                Assert.NotNull(await repository.GetAsync(module));
                await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(Guid.NewGuid()));
            }
            finally { Directory.Delete(root, true); }
        }

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
                var defaultProfile = original.Configuration.Profiles.Single(x => x.DeviceId == "tm-mfd");
                const string axisDefaults = "local diff = { [\"axisDiffs\"] = { [\"a2002cdnil\"] = { [\"name\"] = \"Roll\", [\"removed\"] = { [1] = { [\"key\"] = \"JOY_X\" }, [2] = { [\"key\"] = \"JOY_Y\" }, [3] = { [\"key\"] = \"JOY_Z\" }, [4] = { [\"key\"] = \"JOY_RZ\" } } } } } return diff";
                original = ModuleAxisDefaults.Import(original, defaultProfile.Id, AssignmentLayer.Module, axisDefaults);
                original = ModuleAxisDefaults.Import(original, defaultProfile.Id, AssignmentLayer.UiLayer, axisDefaults);
                var connection = "Data Source=" + Path.Combine(root, "original.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.CreateAsync(original);
                var saved = (await repository.GetAsync(original.Configuration.Module.Id))!;
                var snapshot = ModuleSnapshotWorkflow.Freeze(saved, "2.3.4");
                var guides = ModuleDocumentationWorkflow.Generate(snapshot);
                Assert.Equal(4, guides.Count);
                Assert.Contains(snapshot.Workspace.Sha256, guides["README.md"], StringComparison.Ordinal);
                Assert.Contains("Pitch custom", guides["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                Assert.Contains("Toggle", guides["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                Assert.Contains("UiLayer", guides["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                Assert.Contains("Deadzone", guides["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                Assert.Contains("No active packaged diff", guides["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                var documentation = Path.Combine(root, "Documentation");
                ModuleDocumentationWorkflow.Write(snapshot, documentation);
                Assert.Throws<IOException>(() => ModuleDocumentationWorkflow.Write(snapshot, documentation));
                foreach (var guide in guides) Assert.Equal(guide.Value, File.ReadAllText(Path.Combine(documentation, guide.Key)));
                Assert.Throws<DirectoryNotFoundException>(() => ModuleDocumentationWorkflow.Write(snapshot, Path.Combine(root, "missing", "docs")));
                var occupied = Path.Combine(root, "occupied");
                File.WriteAllText(occupied, "keep");
                Assert.Throws<IOException>(() => ModuleDocumentationWorkflow.Write(snapshot, occupied));
                Assert.Equal("keep", File.ReadAllText(occupied));
                Assert.Empty(Directory.GetDirectories(root, ".ipia-docs-*"));
                var first = ModuleSnapshotWorkflow.Build(snapshot);
                var complete = ModuleCompleteReleaseWorkflow.Build(snapshot);
                Assert.Equal(snapshot, ModuleCompleteReleaseWorkflow.Verify(complete));
                var completeFiles = CompleteReleaseVerifier.Verify(complete);
                Assert.Equal(first.Bytes, completeFiles["OVGME/" + first.FileName]);
                var tampered = completeFiles.Where(x => x.Key != "SHA256SUMS.txt").ToDictionary(x => x.Key, x => x.Value);
                tampered["Documentation/CONTROL-MAPPINGS.md"] = Encoding.UTF8.GetBytes("Stale guides");
                Assert.Throws<InvalidDataException>(() => ModuleCompleteReleaseWorkflow.Verify(CompleteReleaseArchive.Build(complete.FileName[..^4], tampered)));
                tampered["Documentation/CONTROL-MAPPINGS.md"] = completeFiles["Documentation/CONTROL-MAPPINGS.md"];
                tampered["VERSION.TXT"] = Encoding.UTF8.GetBytes("wrong\n");
                Assert.Throws<InvalidDataException>(() => ModuleCompleteReleaseWorkflow.Verify(CompleteReleaseArchive.Build(complete.FileName[..^4], tampered)));
                tampered["VERSION.TXT"] = completeFiles["VERSION.TXT"];
                tampered["local.db"] = [1];
                Assert.Throws<InvalidDataException>(() => ModuleCompleteReleaseWorkflow.Verify(CompleteReleaseArchive.Build(complete.FileName[..^4], tampered)));
                Assert.DoesNotContain(completeFiles.Keys, x => x.EndsWith(".db", StringComparison.Ordinal));
                var completeFolder = Path.Combine(root, "Complete");
                ModuleCompleteReleaseWorkflow.Write(snapshot, completeFolder);
                Assert.Equal(complete.Bytes, File.ReadAllBytes(Path.Combine(completeFolder, complete.FileName)));
                Assert.Equal(complete.ChecksumLine, File.ReadAllText(Path.Combine(completeFolder, "SHA256SUMS.txt")));
                Assert.Throws<IOException>(() => ModuleCompleteReleaseWorkflow.Write(snapshot, completeFolder));
                Assert.Throws<DirectoryNotFoundException>(() => ModuleCompleteReleaseWorkflow.Write(snapshot, Path.Combine(root, "absent", "Complete")));
                Assert.Empty(Directory.GetDirectories(root, ".ipia-complete-*"));
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
                Assert.Equal(guides.OrderBy(x => x.Key), ModuleDocumentationWorkflow.Generate(read).OrderBy(x => x.Key));
                Assert.Equal(guides.OrderBy(x => x.Key), ModuleDocumentationWorkflow.Generate(ModuleSnapshotWorkflow.Freeze(reopened, "2.3.4")).OrderBy(x => x.Key));
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
                Assert.Equal(complete.Bytes, ModuleCompleteReleaseWorkflow.Build(ModuleSnapshotWorkflow.Freeze(reopened, "2.3.4")).Bytes);
                Assert.Equal(first.Bytes, second.Bytes);
                Assert.Contains(axisDefaults, reopened.Sources!.Values);
                var payload = SnapshotLuaPayloadRenderer.Generate(reopened);
                foreach (var scope in new[] { "ROUNDTRIP", "UiLayer" })
                {
                    var removals = DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(payload["Config/Input/" + scope + "/joystick/" + defaultProfile.SourceName]), defaultProfile.Id);
                    Assert.Equal(4, removals.Count(x => x.Section == BindingSection.Axis && x.Disposition == BindingDisposition.Removed));
                    Assert.DoesNotContain(removals, x => x.Section == BindingSection.Axis && x.Disposition != BindingDisposition.Removed);
                }
                Assert.Equal(first.ChecksumLine, second.ChecksumLine);
                var changed = reopened with { Labels = reopened.Labels.ToDictionary(x => x.Key, _ => "Changed label") };
                Assert.Contains("Changed label", ModuleDocumentationWorkflow.Generate(ModuleSnapshotWorkflow.Freeze(changed, "2.3.4"))["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                Assert.Contains("Pitch custom", ModuleDocumentationWorkflow.Generate(read)["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
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

        /// <summary>A pinned consumer guide survives SQLite, portable restore and Complete export with identical deployable bytes.</summary>
        /// <returns>Completion of isolated persistence, documentation and archive checks.</returns>
        [Fact]
        public async Task Imported_consumer_instructions_survive_restore_and_only_change_documentation()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-integration-roundtrip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var workspace = await WorkspaceAsync();
                var before = ModuleSnapshotWorkflow.Freeze(workspace, "1.2.3");
                var reference = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Integrations", "huey-OPENKNEEBOARD-VAICOM.md");
                var source = File.ReadAllText(reference);
                // SHA-256 pins the exact legacy guide, including its historical page inventory.
                var bytes = Encoding.UTF8.GetBytes(source);
                Assert.Equal("24c847bd0b2fb63c0a8d9f7f6790b3d170c6cbac29616dcd966bec9219099065", Convert.ToHexStringLower(SHA256.HashData(bytes)));
                var imported = ModuleIntegrationImport.Apply(workspace, ModuleIntegrationImport.ReadFile(workspace, reference));
                var connection = "Data Source=" + Path.Combine(root, "source.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.CreateAsync(imported);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(source, Assert.Single(ModuleIntegrationImport.Instructions(reopened)).Instructions);
                var snapshot = ModuleSnapshotWorkflow.Freeze(reopened, "1.2.3");
                Assert.Equal(ModuleSnapshotWorkflow.Build(before).Bytes, ModuleSnapshotWorkflow.Build(snapshot).Bytes);
                var guides = ModuleDocumentationWorkflow.Generate(snapshot);
                Assert.Equal(5, guides.Count);
                Assert.Contains("huey-OPENKNEEBOARD-VAICOM", guides["INTEGRATIONS.md"], StringComparison.Ordinal);
                Assert.Contains("does not bundle an AutoHotkey/VAICOM PTT bridge", guides["INTEGRATIONS.md"], StringComparison.Ordinal);
                Assert.Contains("earlier layout", guides["INTEGRATIONS.md"], StringComparison.Ordinal);
                foreach (var line in source.Split('\n')) Assert.Contains("    " + line, guides["INTEGRATIONS.md"], StringComparison.Ordinal);
                var portable = ModuleSnapshotWorkflow.Read(ModuleSnapshotWorkflow.Export(snapshot));
                var target = "Data Source=" + Path.Combine(root, "empty.db") + ";Pooling=False";
                await new SqliteMigrationRunner(target).MigrateAsync();
                var restored = new SqliteEditorWorkspaceRepository(target);
                await ModuleSnapshotWorkflow.RestoreAsync(portable, restored);
                var after = (await restored.GetAsync(workspace.Configuration.Module.Id))!;
                var complete = ModuleCompleteReleaseWorkflow.Build(snapshot);
                Assert.Equal(complete.Bytes, ModuleCompleteReleaseWorkflow.Build(ModuleSnapshotWorkflow.Freeze(after, "1.2.3")).Bytes);
                var files = CompleteReleaseVerifier.Verify(complete);
                Assert.Equal(guides["INTEGRATIONS.md"], Encoding.UTF8.GetString(files["Documentation/INTEGRATIONS.md"]));
                Assert.Equal(snapshot, ModuleCompleteReleaseWorkflow.Verify(complete));
                Assert.DoesNotContain(files.Keys, x => x.EndsWith(".ahk", StringComparison.Ordinal));
                var removed = ModuleIntegrationImport.Remove(after, "huey-OPENKNEEBOARD-VAICOM");
                Assert.DoesNotContain("PTT bridge", ModuleDocumentationWorkflow.Generate(ModuleSnapshotWorkflow.Freeze(removed, "1.2.3"))["INTEGRATIONS.md"], StringComparison.Ordinal);
                Assert.Equal(complete.Bytes, ModuleCompleteReleaseWorkflow.Build(snapshot).Bytes);
                var invalid = after with { Sources = new Dictionary<string, string>(after.Sources!) { ["config/integrations.json"] = "{}" } };
                Assert.Throws<InvalidDataException>(() => ModuleSnapshotWorkflow.Freeze(invalid, "1.2.3"));
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
                var other = workspace with
                {
                    Configuration = new(new(Guid.NewGuid(), "OTHER", "Other module"), [], [], [], "other"),
                    Pages = [],
                    Assignments = [],
                    Diagrams = []
                };
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
                await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(workspace with
                {
                    Configuration = new(new(Guid.Empty, "INVALID", "Invalid"), [], [], [], "invalid")
                }));
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
            Assert.Throws<ArgumentException>(() => ModuleSnapshotWorkflow.Freeze(workspace with
            {
                Images = [new("bad", "bad.png", [1], "bad")]
            }, "1"));
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
                Assert.Throws<InvalidDataException>(() => ModuleDocumentationWorkflow.Write(corrupt, artifact));
                Assert.False(File.Exists(artifact));
            }
            finally { File.Delete(path); File.Delete(artifact); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<EditorWorkspaceSnapshot> WorkspaceAsync()
        {
            var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
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
            workspace = workspace with
            {
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
