using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies native identity discovery, explicit hardware matching, and saved physical device exports.</summary>
    public sealed class DetectedDeviceDiscoveryTests
    {
        private static readonly Guid First = Guid.Parse("C5BE49A0-2342-11EE-8001-444553540000");
        private static readonly Guid Second = Guid.Parse("C5BE49A1-2342-11EE-8001-444553540000");
        #region BEGIN Public
        /// <summary>Native filenames use the physical GUID and preserve captured DCS names.</summary>
        [Fact]
        public void UsesNativePhysicalGuidAndCapturedFileName()
        {
            var device = new DetectedDcsDevice(First, "F16 MFD 3", true);
            Assert.Equal("F16 MFD 3 {C5BE49A0-2342-11ee-8001-444553540000}.diff.lua", device.FileName);
            Assert.Equal(First, DcsPhysicalIdentity.FromFileName(device.FileName));
            Assert.Equal("F16 MFD 3", DcsPhysicalIdentity.NameFromFileName(device.FileName));
            Assert.Contains("Connected", device.Title);
            var captured = device with { Connected = false, CapturedFileName = "Custom MFD {C5BE49A0-2342-11ee-8001-444553540000}.diff.lua" };
            Assert.Equal(captured.CapturedFileName, captured.FileName);
            Assert.Contains("Saved profile", captured.Title);
        }
        /// <summary>The reported TQS GUID uses DCS casing while explicitly captured names remain byte-for-byte intact.</summary>
        [Fact]
        public void PreservesDcsNativeGuidSpelling()
        {
            var physical = Guid.Parse("C0A33440-3F54-11F1-8001-444553540000");
            var detected = new DetectedDcsDevice(physical, "Viper TQS", true);
            Assert.Equal("Viper TQS {C0A33440-3F54-11f1-8001-444553540000}.diff.lua", detected.FileName);
            var captured = "Viper TQS {c0a33440-3f54-11f1-8001-444553540000}.diff.lua";
            Assert.Equal(captured, (detected with { CapturedFileName = captured }).FileName);
            var mixed = new DetectedDcsDevice(Guid.Parse("51FA60C0-CB32-11ED-800B-ABCDEF123456"), "MFD", true);
            Assert.Equal("MFD {51FA60C0-CB32-11ed-800B-ABCDEF123456}.diff.lua", mixed.FileName);
        }
        /// <summary>Unqualified, malformed, or empty GUIDs cannot become physical identities.</summary>
        /// <param name="file">Invalid filename.</param>
        [Theory]
        [InlineData("Device.diff.lua")]
        [InlineData("{C5BE49A0-2342-11EE-8001-444553540000}.diff.lua")]
        [InlineData("Device {}.diff.lua")]
        [InlineData("Device {invalid}.diff.lua")]
        [InlineData("Device {00000000-0000-0000-0000-000000000000}.diff.lua")]
        [InlineData("Device {C5BE49A0-2342-11EE-8001-444553540000}.txt")]
        public void RejectsUnqualifiedIdentity(string file)
        {
            Assert.Null(DcsPhysicalIdentity.FromFileName(file));
            Assert.Throws<ArgumentException>(() => DcsPhysicalIdentity.NameFromFileName(file));
        }
        /// <summary>Repeated devices remain separate choices while duplicate captures merge deterministically.</summary>
        [Fact]
        public void MergesByPhysicalGuidWithoutCollapsingRepeatedUnits()
        {
            var connected = new[] { new DetectedDcsDevice(First, "MFD", true), new(Second, "MFD", true), new(Guid.Empty, "Invalid", true), new(Guid.NewGuid(), " ", true) };
            var file = new DetectedDcsDevice(First, "MFD 3", false, "MFD 3 {" + First + "}.diff.lua");
            var choices = DcsPhysicalIdentity.Merge(connected, [file, file]);
            Assert.Equal(2, choices.Count);
            Assert.True(choices.Single(x => x.PhysicalGuid == First).Connected);
            Assert.Equal(file.FileName, choices.Single(x => x.PhysicalGuid == First).FileName);
            Assert.Equal(choices, DcsPhysicalIdentity.Merge(connected.Reverse(), [file]));
            Assert.Empty(DcsPhysicalIdentity.Merge([], []));
        }
        /// <summary>Disconnected discovery reads filenames only, ignores keyboard/unqualified files, and reports missing roots.</summary>
        [Fact]
        public void FindsCapturedJoystickProfilesWithoutReadingBindings()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-device-scan-" + Guid.NewGuid().ToString("N"));
            try
            {
                var file = new DetectedDcsDevice(First, "MFD 3", false).FileName;
                foreach (var module in new[] { "F-16C_50", "FA-18C_hornet" })
                {
                    var folder = Path.Combine(root, "DCS", "Config", "Input", module, "joystick");
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, file), "not Lua: discovery only reads names");
                    File.WriteAllText(Path.Combine(folder, "Unknown.diff.lua"), "");
                }
                var keyboard = Path.Combine(root, "DCS", "Config", "Input", "UiLayer", "keyboard");
                Directory.CreateDirectory(keyboard);
                File.WriteAllText(Path.Combine(keyboard, new DetectedDcsDevice(Second, "Keyboard", false).FileName), "");
                var found = Assert.Single(SavedGamesDeviceDiscovery.Discover(root));
                Assert.Equal(First, found.PhysicalGuid);
                Assert.False(found.Connected);
                Assert.Equal(file, found.FileName);
                Assert.Equal(found, Assert.Single(SavedGamesDeviceDiscovery.Discover(Path.Combine(root, "DCS"))));
                Assert.Throws<DirectoryNotFoundException>(() => SavedGamesDeviceDiscovery.Discover(Path.Combine(root, "missing")));
                Assert.Throws<ArgumentException>(() => SavedGamesDeviceDiscovery.Discover(" "));
                Directory.Delete(Path.Combine(root, "DCS"), true);
                Assert.Empty(SavedGamesDeviceDiscovery.Discover(root));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>Recognition follows pinned rule order, leaving unmatched or ambiguous hardware for user selection.</summary>
        [Fact]
        public void RecognizesOnlyPinnedOrUniqueHardwareMatches()
        {
            var saved = Workspace();
            var hardware = Hardware();
            var other = hardware with { DeviceId = "other" };
            var sources = new Dictionary<string, string>(saved.Sources!) {
                ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { hardware, other }),
                ["config/ipia-device-map.json"] = JsonSerializer.Serialize(new SharedDeviceMap(
                    [new("tm-mfd", ["F16 MFD"], null, null), new("other", ["MFD"], null, null)],
                    new Dictionary<string, IReadOnlyDictionary<string, string>>(), []))
            };
            saved = saved with { Sources = sources };
            Assert.Equal("tm-mfd", DetectedDeviceHardwareMatcher.Match(saved, new(First, "F16 MFD 3", true))!.DeviceId);
            Assert.Equal("other", DetectedDeviceHardwareMatcher.Match(saved, new(First, "Unknown MFD", true))!.DeviceId);
            Assert.Null(DetectedDeviceHardwareMatcher.Match(saved, new(First, "Unknown grip", true)));
            sources.Remove("config/ipia-device-map.json");
            Assert.Null(DetectedDeviceHardwareMatcher.Match(saved, new(First, "F16 MFD 3", true)));
            sources["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { hardware });
            Assert.Equal("tm-mfd", DetectedDeviceHardwareMatcher.Match(saved, new(Second, "F16 MFD 3", true))!.DeviceId);
            sources["config/ipia-device-map.json"] = "null";
            Assert.NotNull(DetectedDeviceHardwareMatcher.Match(saved, new(Second, "F16 MFD 3", true)));
        }
        /// <summary>Separate physical MFDs can omit instance and role, while both scopes reuse the selected physical identity.</summary>
        [Fact]
        public void AddsRepeatedPhysicalUnitsWithOptionalIdentityAndExportsNativeNames()
        {
            var saved = Workspace();
            var hardware = Hardware();
            var first = new DetectedDcsDevice(First, "F16 MFD 3", true);
            var second = new DetectedDcsDevice(Second, "F16 MFD 3", true);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, first.FileName, null, null);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, second.FileName, null, null);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.UiLayer, first.FileName, null, null);
            Assert.Equal(2, saved.Configuration.Profiles.Count);
            var memberships = WorkspaceStructureEditor.Describe(saved).Devices;
            Assert.All(memberships, x => { Assert.Null(x.Instance); Assert.Null(x.Role); Assert.NotEqual(x.ProfileId, x.PhysicalGuid); });
            Assert.Single(memberships.Select(x => x.ProfileId).Distinct(), id => memberships.Count(x => x.ProfileId == id) == 2);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, first.FileName, "Another", null));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, hardware with { DeviceId = "other" }, AssignmentLayer.Module, "Other {" + First + "}.diff.lua", null, null));
            var device = memberships.First(x => x.PhysicalGuid == First && x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.SetModifier(saved, null, new(AssignmentLayer.Module, device.ProfileId, "SHIFT", "JOY_BTN1", "Shift", ModifierMode.Hold, ""));
            var renamed = "Edited MFD {" + First + "}.diff.lua";
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, renamed, null, null);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, "Edited.diff.lua", null, null));
            var payload = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("Config/Input/TEST/joystick/" + renamed, payload.Keys);
            Assert.Contains("Config/Input/TEST/joystick/" + second.FileName, payload.Keys);
            Assert.Contains("Config/Input/UiLayer/joystick/" + first.FileName, payload.Keys);
            Assert.Equal(renamed[..^9], Assert.Single(DcsLuaParser.ParseModifiers(Encoding.UTF8.GetString(payload["Config/Input/TEST/modifiers.lua"])), x => x.Device != "Keyboard").Device);
        }
        /// <summary>Native GUID membership survives a real SQLite reopen without substituting the workspace profile ID.</summary>
        /// <returns>Completion of the persistence round trip.</returns>
        [Fact]
        public async Task PersistsPhysicalGuidAcrossSqliteReopen()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-guid-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var saved = WorkspaceStructureEditor.AddDevice(Workspace(), Hardware(), AssignmentLayer.Module,
                    new DetectedDcsDevice(First, "F16 MFD 3", true).FileName, null, null);
                var connection = "Data Source=" + root + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var reopened = (await repository.GetAsync(saved.Configuration.Module.Id))!;
                Assert.Equal(First, Assert.Single(WorkspaceStructureEditor.Describe(reopened).Devices).PhysicalGuid);
                Assert.Equal(SnapshotLuaPayloadRenderer.Generate(saved).Keys, SnapshotLuaPayloadRenderer.Generate(reopened).Keys);
            }
            finally { File.Delete(root); }
        }
        /// <summary>Physical/drawing unlink are independent, preserve authored state and survive serialization.</summary>
        [Fact]
        public void Explicit_unlink_preserves_bindings_and_requires_a_new_physical_link_before_export()
        {
            var workspace = WorkspaceStructureEditor.AddDevice(Workspace(), Hardware(), AssignmentLayer.Module,
                new DetectedDcsDevice(First, "F16 MFD 3", true).FileName, null, null);
            workspace = WorkspaceStructureEditor.AddDevice(workspace, Hardware(), AssignmentLayer.UiLayer,
                new DetectedDcsDevice(First, "F16 MFD 3", true).FileName, null, null);
            var device = WorkspaceStructureEditor.Describe(workspace).Devices[0];
            workspace = workspace with { Assignments = [new(new(workspace.Configuration.Module.Id, device.ProfileId,
                AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", ""),
                new("d3001pnilu3001cdnilvd1vpnilvunil", "Test", false, AssignmentLayer.Module))] };
            var original = JsonSerializer.Serialize(workspace);
            var unlinked = WorkspaceStructureEditor.UnlinkPhysicalDevice(workspace, device);
            Assert.All(WorkspaceStructureEditor.Describe(unlinked).Devices, x => Assert.Null(x.PhysicalGuid));
            Assert.Equal(workspace.Assignments, unlinked.Assignments);
            Assert.Equal(workspace.Diagrams, unlinked.Diagrams);
            Assert.Throws<InvalidDataException>(() => ModulePhysicalBindingReview.Validate(unlinked));
            var detachedDrawing = WorkspaceStructureEditor.SetDrawingLinked(unlinked, device.ProfileId, false);
            var frozen = EditorSnapshotSerializer.Serialize(detachedDrawing);
            var reopened = EditorSnapshotSerializer.Deserialize(frozen);
            Assert.Equal(frozen, EditorSnapshotSerializer.Serialize(reopened));
            Assert.Equal(detachedDrawing.Assignments, reopened.Assignments);
            Assert.All(WorkspaceStructureEditor.Describe(reopened).Devices, x => Assert.Null(x.PhysicalGuid));
            Assert.True(reopened.Diagrams.Single().DrawingUnlinked);
            var current = WorkspaceStructureEditor.Describe(reopened).Devices[0];
            var relinked = WorkspaceStructureEditor.RebindDevice(reopened, current,
                new DetectedDcsDevice(Second, "F16 MFD 3", true).FileName, null, null);
            Assert.All(WorkspaceStructureEditor.Describe(relinked).Devices, x => Assert.Equal(Second, x.PhysicalGuid));
            Assert.True(relinked.Diagrams.Single().DrawingUnlinked);
            Assert.False(WorkspaceStructureEditor.SetDrawingLinked(relinked, device.ProfileId, true).Diagrams.Single().DrawingUnlinked);
            Assert.Equal(original, JsonSerializer.Serialize(workspace));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceHardware Hardware() => new("tm-mfd", "MFD", new DetectedDcsDevice(First, "F16 MFD 3", false).FileName,
            new UiLayerDiagram("MFD", null, "mfd.svg", 100, 100, [], []) { Controls = [new("one", "OSB01", "JOY_BTN1", 0, 0, 10, 10, false)] });
        private static EditorWorkspaceSnapshot Workspace() => new(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "TEST", "Test"), [], [], [], "fixture"),
            [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { ["config/kneeboard.json"] = "{\"profiles\":{},\"pages\":[]}", ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { Hardware() }) });
        #endregion END private helpers
    }
}
