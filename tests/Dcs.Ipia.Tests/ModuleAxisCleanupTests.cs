using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks unwanted default axes on authored and unused native controllers through saved package generation.</summary>
    public sealed class ModuleAxisCleanupTests
    {
        private static readonly Guid ModuleId = Guid.Parse("33249084-6604-4674-a684-ddcd03567a65");
        private static readonly Guid StickId = Guid.Parse("83564d2f-c79f-46ac-b576-9c7a93267c47");
        private const string Stick = "Stick {00000000-0000-0000-0000-000000000001}.diff.lua";
        private const string Unused = "Unused quadrant {00000000-0000-0000-0000-000000000002}.diff.lua";
        private const string CleanupSource = "config/ipia-axis-cleanup-devices.json";
        #region BEGIN Public
        /// <summary>Unassigned axes disappear even on controllers without a module device or graphic.</summary>
        /// <param name="imported">Whether the module lacks the locally created marker.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GeneratedPayloadSuppressesUnusedControllersAndPreservesAssignedAxes(bool imported)
        {
            var original = Workspace();
            if (imported) original = original with { Sources = original.Sources!
                .Where(x => x.Key != "config/ipia-created-module.json").ToDictionary(x => x.Key, x => x.Value) };
            var workspace = ModuleAxisCleanup.CaptureDevices(original, [Stick, Unused]);
            var output = SnapshotLuaPayloadRenderer.Generate(workspace);
            var stick = Bindings(output, Stick);
            Assert.Contains(stick, x => x.CommandId == "a2001cdnil" && x.InputKey == "JOY_Y" &&
                x.Disposition == BindingDisposition.Added && x.Filter?.Deadzone == 4);
            Assert.DoesNotContain(stick, x => x.CommandId == "a2001cdnil" && x.InputKey == "JOY_Y" &&
                x.Reformers.Count == 0 && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(stick, x => x.CommandId == "a2002cdnil" && x.InputKey == "JOY_X" &&
                x.Reformers.SingleOrDefault() == "SHIFT" && x.Disposition == BindingDisposition.Added);
            Assert.Contains(stick, x => x.CommandId == "a2002cdnil" && x.InputKey == "JOY_X" &&
                x.Reformers.Count == 0 && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(stick, x => x.CommandId == "KEY" && x.Disposition == BindingDisposition.Added);
            var unused = Bindings(output, Unused);
            Assert.Equal(16, unused.Count);
            Assert.All(unused, x => Assert.Equal(BindingDisposition.Removed, x.Disposition));
            Assert.All(unused, x => Assert.Equal(BindingSection.Axis, x.Section));
            Assert.Single(workspace.Configuration.Profiles);
            Assert.Single(workspace.Diagrams);
            Assert.Equal(original.Assignments, workspace.Assignments);
            Assert.DoesNotContain(CleanupSource, original.Sources!.Keys);
            Assert.Equal(output["Config/Input/UiLayer/joystick/" + Stick],
                SnapshotLuaPayloadRenderer.Generate(original)["Config/Input/UiLayer/joystick/" + Stick]);
            Assert.Same(workspace, ModuleAxisCleanup.CaptureDevices(workspace, [Unused, Stick]));
            var repeated = SnapshotLuaPayloadRenderer.Generate(workspace);
            foreach (var pair in output) Assert.Equal(pair.Value, repeated[pair.Key]);
        }

        /// <summary>Real SQLite reopening, portable restore and OvGME rebuild retain removal-only profiles.</summary>
        /// <param name="imported">Whether the saved module was imported rather than created locally.</param>
        /// <returns>Completion of the source-free package round trip.</returns>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupSurvivesDatabaseSnapshotAndPackageRoundTrip(bool imported)
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-axis-cleanup-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var original = Workspace();
                if (imported) original = original with { Sources = original.Sources!
                    .Where(x => x.Key != "config/ipia-created-module.json").ToDictionary(x => x.Key, x => x.Value) };
                var workspace = ModuleAxisCleanup.CaptureDevices(original, [Stick, Unused]);
                await repository.SaveAsync(workspace);
                var saved = (await repository.GetAsync(ModuleId))!;
                var snapshot = ModuleSnapshotWorkflow.Freeze(saved, "1.0");
                var restored = ModuleSnapshotWorkflow.Read(ModuleSnapshotWorkflow.Export(snapshot));
                var first = ModuleSnapshotWorkflow.Build(snapshot);
                Assert.Equal(first.Bytes, ModuleSnapshotWorkflow.Build(restored).Bytes);
                using var stream = new MemoryStream(first.Bytes);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                Assert.Contains(zip.Entries, x => x.FullName.EndsWith("/Config/Input/AXIS-TEST/joystick/" + Unused, StringComparison.Ordinal));
                var docs = ModuleDocumentationWorkflow.Generate(restored);
                Assert.Contains(docs.Values, text => text.Contains(Unused, StringComparison.Ordinal));
                Assert.Equal(workspace.Assignments.Count, saved.Assignments.Count);
            }
            finally { File.Delete(path); }
        }

        /// <summary>Offline identities survive rescans while current module filenames win over stale discovery spelling.</summary>
        [Fact]
        public void CapturePreservesOfflineUnitsAndUsesManagedNativeFileName()
        {
            var workspace = ModuleAxisCleanup.CaptureDevices(Workspace(), [Unused]);
            var stale = Stick.Replace("Stick {", "Old name {", StringComparison.Ordinal);
            workspace = ModuleAxisCleanup.CaptureDevices(workspace, [stale]);
            var output = SnapshotLuaPayloadRenderer.Generate(workspace);
            Assert.Contains("Config/Input/AXIS-TEST/joystick/" + Stick, output.Keys);
            Assert.DoesNotContain("Config/Input/AXIS-TEST/joystick/" + stale, output.Keys);
            Assert.Contains("Config/Input/AXIS-TEST/joystick/" + Unused, output.Keys);
            Assert.Same(workspace, ModuleAxisCleanup.CaptureDevices(workspace, []));
        }

        /// <summary>Known saved assignments and imported bindings supply exact IDs even without a separate catalog.</summary>
        [Fact]
        public void ImportedModuleWithoutCatalogCleansUnusedControllersFromSavedEvidence()
        {
            var workspace = Workspace();
            var sources = workspace.Sources!.Where(x => x.Key != "config/ipia-created-module.json").ToDictionary(x => x.Key, x => x.Value);
            var configuration = workspace.Configuration;
            var imported = workspace with { Sources = sources, ModuleCommands = [], Configuration = new(configuration.Module,
                configuration.Profiles, configuration.Modifiers,
                [new(StickId, BindingSection.Axis, "a2003cdnil", "Rudder", BindingDisposition.Removed, "JOY_RZ")], configuration.Provenance) };
            var captured = ModuleAxisCleanup.CaptureDevices(imported, [Unused]);
            var bindings = Bindings(SnapshotLuaPayloadRenderer.Generate(captured), Unused);
            Assert.Equal(24, bindings.Count);
            Assert.All(bindings, x => Assert.Equal(BindingDisposition.Removed, x.Disposition));
            Assert.Equal(new[] { "a2001cdnil", "a2002cdnil", "a2003cdnil" }, bindings.Select(x => x.CommandId).Distinct().Order(StringComparer.Ordinal));
            Assert.DoesNotContain(CleanupSource, imported.Sources!.Keys);
        }

        /// <summary>Imported axes owned only by UI Layer cannot supply aircraft cleanup commands.</summary>
        [Fact]
        public void UiOnlyImportedAxesDoNotBecomeAircraftCleanupCommands()
        {
            var original = Workspace();
            var uiId = Guid.NewGuid();
            var uiFile = "UI controller {00000000-0000-0000-0000-000000000003}.diff.lua";
            var sources = original.Sources!.ToDictionary(x => x.Key, x => x.Value);
            sources.Remove("config/ipia-created-module.json");
            sources[WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(new WorkspaceManagement([
                new(StickId, AssignmentLayer.Module, Stick, null, "Stick"),
                new(uiId, AssignmentLayer.UiLayer, uiFile, null, "UI controller")], []));
            var configuration = original.Configuration;
            var imported = original with { Sources = sources, ModuleCommands = [], Assignments = [], Configuration = new(configuration.Module,
                [.. configuration.Profiles, new DeviceProfile(uiId, "ui", null, "UI controller", uiFile)], [], [
                    new(StickId, BindingSection.Axis, "a2001cdnil", "Pitch", BindingDisposition.Removed, "JOY_Y"),
                    new(uiId, BindingSection.Axis, "UI-ONLY", "UI axis", BindingDisposition.Removed, "JOY_X")
                ], "Imported") };
            var captured = ModuleAxisCleanup.CaptureDevices(imported, [Unused]);
            var payload = new Dictionary<string, byte[]>();
            ModuleAxisCleanup.CompletePayload(captured, payload);
            var bindings = Bindings(payload, Unused);
            Assert.Equal(8, bindings.Count);
            Assert.All(bindings, x => Assert.Equal("a2001cdnil", x.CommandId));
        }

        /// <summary>Uncaptured snapshots keep their output and unknown commands do not create guessed removals.</summary>
        [Fact]
        public void UncapturedAndUnknownCommandsKeepExistingOutput()
        {
            var workspace = Workspace() with { ModuleCommands = [], Assignments = [] };
            var original = SnapshotLuaPayloadRenderer.Generate(workspace);
            var noCommands = ModuleAxisCleanup.CaptureDevices(workspace, [Unused]);
            var output = SnapshotLuaPayloadRenderer.Generate(noCommands);
            foreach (var pair in original) Assert.Equal(pair.Value, output[pair.Key]);
            Assert.Equal(original.Count, output.Count);
        }

        /// <summary>Unused units can be captured without existing sources while generic legacy files remain intact.</summary>
        [Fact]
        public void CaptureSupportsMissingSourcesAndDoesNotRewriteLegacyFileNames()
        {
            var original = Workspace() with { Sources = null };
            var captured = ModuleAxisCleanup.CaptureDevices(original, [Unused]);
            Assert.Null(original.Sources);
            Assert.Contains(Unused, captured.Sources![CleanupSource]);
            const string legacy = "Config/Input/AXIS-TEST/joystick/Stick.diff.lua";
            var bytes = Encoding.UTF8.GetBytes("local diff = {} return diff");
            var payload = new Dictionary<string, byte[]> { [legacy] = bytes };
            ModuleAxisCleanup.CompletePayload(captured, payload);
            Assert.Same(bytes, payload[legacy]);
            Assert.Equal(16, Bindings(payload, Unused).Count);
        }

        /// <summary>Invalid paths, empty JSON and duplicate native units cannot become deployable file targets.</summary>
        /// <param name="file">Unsafe or nonnative filename.</param>
        [Theory]
        [InlineData("../Controller {00000000-0000-0000-0000-000000000002}.diff.lua")]
        [InlineData("Controller.diff.lua")]
        [InlineData("")]
        public void RejectsUnsafeCapturedIdentities(string file)
        {
            var workspace = Workspace();
            Assert.Throws<InvalidDataException>(() => ModuleAxisCleanup.CaptureDevices(workspace, [file]));
            foreach (var json in new[] { "null", JsonSerializer.Serialize(new[] { Unused, Unused }) })
            {
                var sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value);
                sources[CleanupSource] = json;
                Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(workspace with { Sources = sources }));
            }
            Assert.DoesNotContain(CleanupSource, workspace.Sources!.Keys);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IReadOnlyList<DcsBinding> Bindings(IReadOnlyDictionary<string, byte[]> output, string file) =>
            DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(output["Config/Input/AXIS-TEST/joystick/" + file]), StickId);
        private static EditorWorkspaceSnapshot Workspace()
        {
            var profile = new DeviceProfile(StickId, "stick", null, "Stick", Stick);
            var module = new DcsModule(ModuleId, "AXIS-TEST", "Axis test");
            var sources = new Dictionary<string, string> {
                ["config/ipia-created-module.json"] = "{}",
                ["config/kneeboard.json"] = """{"profiles":{"stick":"Stick {00000000-0000-0000-0000-000000000001}.diff.lua"},"pages":[{"profile":"stick","file":"01-STICK","title":"Stick"}],"summaryPages":[{"type":"summary","file":"00-OVERVIEW","title":"Overview","kicker":"CONTROLS","items":[{"key":"STICK","text":"Flight controls","accent":"cyan"}]}]}""",
                ["config/ipia-ui-" + StickId.ToString("N") + ".lua"] = "local diff = {} return diff",
                [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(new WorkspaceManagement([
                    new(StickId, AssignmentLayer.Module, Stick, null, "Stick"),
                    new(StickId, AssignmentLayer.UiLayer, Stick, null, "Stick")
                ], [new(AssignmentLayer.Module, StickId, "SHIFT", "JOY_BTN3", "Shift", ModifierMode.Hold, Stick[..^9])]))
            };
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [], []) { ProfileId = StickId, Controls = [
                new("pitch", "Pitch", "JOY_Y", 0, 0, 10, 10, true),
                new("roll", "Roll", "JOY_X", 0, 0, 10, 10, true),
                new("key", "Key", "JOY_BTN1", 0, 0, 10, 10)
            ], Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-pitch' x='10' y='20' fill='white'>Pitch</text><text id='lbl-roll' x='10' y='40' fill='white'>Roll</text><text id='lbl-key' x='10' y='60' fill='white'>Key</text></svg>" };
            var assignments = new DraftAssignment[] {
                new(new(ModuleId, StickId, AssignmentLayer.Module, BindingSection.Axis, "JOY_Y"),
                    new("a2001cdnil", "Pitch", true, AssignmentLayer.Module)) { Filter = new(Deadzone: 4) },
                new(new(ModuleId, StickId, AssignmentLayer.Module, BindingSection.Axis, "JOY_X", "SHIFT"),
                    new("a2002cdnil", "Roll", true, AssignmentLayer.Module)),
                new(new(ModuleId, StickId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"),
                    new("KEY", "Key", false, AssignmentLayer.Module))
            };
            var page = new KneeboardPage(Guid.Parse("ee5b640b-8f22-493d-9c77-1454b13171b4"), ModuleId, null,
                KneeboardPageKind.Summary, "00-OVERVIEW", "Overview", [], 0);
            var devicePage = new KneeboardPage(Guid.Parse("f594130b-a7ca-4bc0-810e-93169e248fb7"), ModuleId, null,
                KneeboardPageKind.Device, "01-STICK", "Stick", [], 1);
            var workspace = new EditorWorkspaceSnapshot(new(module, [profile],
                [new("SHIFT", Stick[..^9], "JOY_BTN3", ModifierMode.Hold)], [], "Created in IPIA"), [page, devicePage], [], [diagram],
                [new("a2001cdnil", "Pitch", "Axes", true), new("a2002cdnil", "Roll", "Axes", true), new("KEY", "Key", "Keys", false)],
                [], assignments, new Dictionary<string, string>(), sources);
            return ModulePhysicalBindingReview.Confirm(workspace, StickId, DcsPhysicalIdentity.FromFileName(Stick)!.Value);
        }
        #endregion END private helpers
    }
}
