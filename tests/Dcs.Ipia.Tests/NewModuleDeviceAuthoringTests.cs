using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies repeated physical units, baseline binding evidence and persisted native output.</summary>
    public sealed class NewModuleDeviceAuthoringTests
    {
        #region BEGIN Public
        /// <summary>Readable UI labels omit duplicate GUIDs while native filenames remain unchanged.</summary>
        [Fact]
        public void FormatsReadableNamesWithoutChangingNativeIdentity()
        {
            var guid = Guid.Parse("2D5CEC70-5189-11F1-8001-444553540000");
            var file = new DetectedDcsDevice(guid, "VKBSim Gunfighter F14", true).FileName;
            Assert.Equal("VKBSim Gunfighter F14", DcsDeviceDisplay.Label(file));
            Assert.Equal("VKBSim Gunfighter F14 · Pilot", DcsDeviceDisplay.Label(file, " Pilot "));
            Assert.Equal("VKBSim Gunfighter F14 · Unit 2d5cec70", DcsDeviceDisplay.Label(file[..^9], guid.ToString("D")));
            Assert.Equal("Plain", DcsDeviceDisplay.Label("Plain", " "));
            Assert.Equal("Plain · Right", DcsDeviceDisplay.Label("Plain.diff.lua", "Right"));
            Assert.Equal(file, new WorkspaceDevice(Guid.NewGuid(), AssignmentLayer.Module, file, "Pilot", null).FileName);
            var diagram = new UiLayerDiagram(file[..^9], guid.ToString("D"), "device.svg", 1, 1, [], []);
            Assert.Equal("VKBSim Gunfighter F14 · Unit 2d5cec70", diagram.Title);
        }
        /// <summary>Reported hardware choices support repeated units, portable review evidence and isolated SQLite output.</summary>
        /// <returns>Completion of real bundled catalog and persistence checks.</returns>
        [Fact]
        public async Task CreatesRepeatedUnitsAndRequiresReviewedBaselineBindings()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-units-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = await new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(await SharedUiLayerWorkspaceFactory.CreateAsync());
                var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                var workspace = NewModuleWorkspaceFactory.Create("P47-D-30", "P-47D Thunderbolt", defaults, hardware);
                var unrelated = NewModuleWorkspaceFactory.Create("UNRELATED", "Other module", defaults, hardware);
                var choices = WorkspaceStructureEditor.Hardware(workspace);
                var initialSources = JsonSerializer.Serialize(unrelated.Sources);
                Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(workspace));
                var inherited = workspace.Configuration.Profiles.First(x => x.DeviceId == "tm-mfd");
                var mfd = choices.Single(x => x.DeviceId == "tm-mfd");
                var firstFile = new DetectedDcsDevice(Guid.NewGuid(), "My MFD", false).FileName;
                var first = new NewModuleDeviceSelection(mfd, firstFile, "Left", "Display", inherited.SourceName);
                Assert.Throws<InvalidOperationException>(() => NewModuleDeviceAuthoring.Apply(workspace, first with { FileName = "Template.diff.lua" }));
                Assert.Throws<InvalidOperationException>(() => NewModuleDeviceAuthoring.Apply(workspace, first with { BaselineFileName = "missing.diff.lua" }));
                Assert.Throws<InvalidOperationException>(() => NewModuleDeviceAuthoring.Apply(workspace, first with { Hardware = choices.Single(x => x.DeviceId == "moza-ab9") }));
                workspace = NewModuleDeviceAuthoring.Apply(workspace, first);
                Assert.Equal(inherited.Id, workspace.Configuration.Profiles.Single(x => x.SourceName == firstFile).Id);
                Assert.Throws<InvalidOperationException>(() => NewModuleDeviceAuthoring.Apply(workspace, first with { BaselineFileName = null }));
                Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RebindDevice(workspace,
                    WorkspaceStructureEditor.Describe(workspace).Devices.First(x => x.ProfileId == inherited.Id), "../" + firstFile, null, null));
                var names = new[] { "tm-mfd", "tm-mfd", "tm-mfd", "logitech-throttle-quadrant", "logitech-throttle-quadrant",
                    "moza-ab9", "vkb-f14-gunfighter", "winctrl-pto2", "winctrl-icp" };
                var added = new List<string> { firstFile };
                for (var i = 0; i < names.Length; i++)
                {
                    var choice = choices.Single(x => x.DeviceId == names[i]);
                    var file = new DetectedDcsDevice(Guid.NewGuid(), choice.Label, false).FileName;
                    workspace = NewModuleDeviceAuthoring.Apply(workspace, new(choice, file, "Unit " + i, null));
                    added.Add(file);
                }
                var tpr = choices.First(x => x.Label.Contains("Pendular", StringComparison.OrdinalIgnoreCase));
                var tprFile = new DetectedDcsDevice(Guid.NewGuid(), "T-Pendular Rudder", false).FileName;
                workspace = NewModuleDeviceAuthoring.Apply(workspace, new(tpr, tprFile, null, null));
                added.Add(tprFile);
                var state = WorkspaceStructureEditor.Describe(workspace);
                Assert.Equal(4, state.Devices.Count(x => x.Layer == AssignmentLayer.Module && workspace.Configuration.Profiles.Single(p => p.Id == x.ProfileId).DeviceId == "tm-mfd"));
                Assert.Equal(2, state.Devices.Count(x => x.Layer == AssignmentLayer.Module && workspace.Configuration.Profiles.Single(p => p.Id == x.ProfileId).DeviceId == "logitech-throttle-quadrant"));
                Assert.Throws<InvalidDataException>(() => ModulePhysicalBindingReview.Validate(workspace));
                foreach (var device in state.Devices.DistinctBy(x => x.ProfileId))
                    workspace = WorkspaceStructureEditor.RebindDevice(workspace, device, device.FileName, device.Instance, device.Role);
                ModulePhysicalBindingReview.Validate(workspace);
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                await repository.SaveAsync(unrelated);
                var restored = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var output = SnapshotLuaPayloadRenderer.Generate(restored);
                foreach (var file in added) Assert.Contains("Config/Input/P47-D-30/joystick/" + file, output.Keys);
                Assert.Contains("Config/Input/UiLayer/joystick/" + firstFile, output.Keys);
                Assert.Equal(defaults.ContentSha256, restored.SharedDefault!.ContentSha256);
                Assert.Equal(initialSources, JsonSerializer.Serialize((await repository.GetAsync(unrelated.Configuration.Module.Id))!.Sources));
                var again = SnapshotLuaPayloadRenderer.Generate(restored);
                foreach (var entry in output) Assert.Equal(entry.Value, again[entry.Key]);
                var broken = restored with { Sources = restored.Sources!.Where(x => x.Key != "config/ipia-physical-bindings.json").ToDictionary(x => x.Key, x => x.Value) };
                Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(broken));
                var plain = restored with { Sources = new Dictionary<string, string>() };
                Assert.Same(plain, ModulePhysicalBindingReview.Confirm(plain, Guid.NewGuid(), Guid.NewGuid()));
                ModulePhysicalBindingReview.Validate(plain);
            }
            finally { File.Delete(path); }
        }
        #endregion END Public
    }
}
