using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    public sealed class WarThunderControllerAdditionTests
    {
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Appending_preserves_native_bytes_bindings_and_existing_ranges(string newline)
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.ReplaceLineEndings(newline);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var beforeBindings = JsonSerializer.Serialize(WarThunderProfileBackend.ReadBindings(profile));
            var added = backend.AddNativeController(profile, "New \"stick\" \\ controller", Observation());
            var devices = WarThunderProfileBackend.ReadDevices(added);
            Assert.Equal(3, devices.Count);
            Assert.Equal(WarThunderProfileBackend.ReadDevices(profile), devices.Take(2));
            var device = devices[2];
            Assert.Equal("1234:5678", device.Product);
            Assert.Equal("New \"stick\" \\ controller", device.Name);
            Assert.Equal(4, device.AxesOffset);
            Assert.Equal(8, device.ButtonsOffset);
            Assert.Equal(3, device.AxesCount); // X plus two POV axes.
            Assert.Equal(6, device.ButtonsCount); // Two driver buttons plus four POV directions.
            Assert.Equal(beforeBindings, JsonSerializer.Serialize(WarThunderProfileBackend.ReadBindings(added)));
            var exported = WarThunderBackendFixture.Export(backend, added);
            var declaration = WarThunderBlkDocument.Parse(exported).Blocks.Single(block => block.Address == device.RecordId);
            var restored = exported.Remove(declaration.HeaderStart - newline.Length,
                declaration.End - declaration.HeaderStart + 2 * newline.Length);
            Assert.Equal(source, restored);
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
            var imported = backend.Import("test-context", new Dictionary<string, string> { ["Test controls.blk"] = exported });
            Assert.Equal(devices, WarThunderProfileBackend.ReadDevices(imported));
            Assert.Equal(exported, WarThunderBackendFixture.Export(backend, imported));
        }

        [Fact]
        public void Empty_mapping_can_be_created_and_unused_captured_indices_are_reserved()
        {
            var backend = new WarThunderProfileBackend();
            var profile = WarThunderBackendFixture.Import(backend, "controls{version:i=5;hotkeys{ID_OLD{joyButton:i=90;}}axes{pitch{axisId:i=30;}}}");
            var added = backend.AddNativeController(profile, "New", Observation());
            var device = Assert.Single(WarThunderProfileBackend.ReadDevices(added));
            Assert.Equal(91, device.ButtonsOffset);
            Assert.Equal(31, device.AxesOffset);
            Assert.Equal(2, WarThunderProfileBackend.ReadBindings(added).Count);
        }

        [Fact]
        public void New_ranges_accept_button_and_axis_assignments_and_reexport()
        {
            var backend = new WarThunderProfileBackend();
            var profile = backend.AddNativeController(WarThunderBackendFixture.Import(backend), "New", Observation());
            var button = WarThunderBackendFixture.Binding(profile, "ID_FIRE");
            var axis = WarThunderBackendFixture.Binding(profile, "pitch");
            var newDevice = WarThunderProfileBackend.ReadDevices(profile)[2];
            var changed = WarThunderProfileBackend.RebindInput(profile, button.Id,
                WarThunderBackendFixture.Field(button, "joyButton").Address, newDevice.Id, CommandType.Button, 1);
            changed = WarThunderProfileBackend.RebindInput(changed, axis.Id,
                WarThunderBackendFixture.Field(axis, "axisId").Address, newDevice.Id, CommandType.Axis, 0);
            var text = WarThunderBackendFixture.Export(backend, changed);
            Assert.Contains("joyButton:i=9;activationType:i=9", text, StringComparison.Ordinal);
            Assert.Contains("axisId:i=4;inverse:b=no;kMul:r=1.25", text, StringComparison.Ordinal);
            Assert.Contains("ID_FIRE{keyboardKey:i=30;keyboardKey:i=31;}", text, StringComparison.Ordinal);
            Assert.Contains("throttle{axisId:i=2;nonlinearity:i=1;}", text, StringComparison.Ordinal);
            var imported = backend.Import("test-context", new Dictionary<string, string> { ["Test controls.blk"] = text });
            Assert.Equal(text, WarThunderBackendFixture.Export(backend, imported));
        }

        [Theory]
        [InlineData("bad-name")]
        [InlineData("missing")]
        [InlineData("disconnected")]
        [InlineData("product")]
        [InlineData("details")]
        [InlineData("empty")]
        [InlineData("ambiguous")]
        [InlineData("invalid-ranges")]
        [InlineData("overlap")]
        [InlineData("multiple-mappings")]
        public void Invalid_creation_leaves_original_profile_unchanged(string failure)
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source;
            if (failure == "invalid-ranges") source = source.Replace("axesOffset:i=0", "axesOffset:i=-1", StringComparison.Ordinal);
            if (failure == "overlap") source = source.Replace("axesOffset:i=2", "axesOffset:i=1", StringComparison.Ordinal);
            if (failure == "multiple-mappings") source = source.Replace("deviceMapping{", "deviceMapping{}deviceMapping{", StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var observation = Observation();
            observation = failure switch
            {
                "missing" => null,
                "disconnected" => observation with { Device = observation.Device with { Connected = false } },
                "product" => observation with { Device = observation.Device with { ProductGuid = Guid.Empty } },
                "details" => observation with { Capabilities = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 4, "Legacy")]) },
                "empty" => observation with { Capabilities = ControllerCapabilityMapper.Map([]) },
                "ambiguous" => observation with { Capabilities = ControllerCapabilityMapper.Map([WarThunderLayoutFixture.Button(0), WarThunderLayoutFixture.Button(0)]) },
                _ => observation
            };
            Assert.ThrowsAny<ArgumentException>(() => backend.AddNativeController(profile, " "));
            Assert.Throws<InvalidOperationException>(() => backend.AddNativeController(profile, "New"));
            Assert.Throws<InvalidOperationException>(() => backend.AddNativeController(profile, failure == "bad-name" ? "bad\nname" : "New", observation));
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
        }

        [Fact]
        public void Review_is_atomic_rechecks_input_evidence_and_supports_save_reopen_and_discard()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new WarThunderProfileBackend();
            var profile = WarThunderBackendFixture.Import(backend);
            var observation = Observation();
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Addition", profile, new());
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [observation.Device],
                controllerInputs: () => [observation]);
            Assert.True(session.CanAddNativeControllers);
            var review = session.PreviewAddNativeController(observation.Device.PhysicalGuid, observation.Device.Name);
            Assert.False(session.HasChanges);
            Assert.Equal(2, session.Snapshot.Profile.Devices.Count);
            var original = observation;
            observation = observation with { Capabilities = ControllerCapabilityMapper.Map([WarThunderLayoutFixture.Button(0)]) };
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(review));
            observation = original;
            session.ApplyControllerChange(review);
            Assert.True(session.HasChanges);
            Assert.Equal(observation.Device.PhysicalGuid, session.Snapshot.Hardware[review.ResultDeviceId!].Device.PhysicalGuid);
            Assert.Throws<InvalidOperationException>(() => session.PreviewAddNativeController(observation.Device.PhysicalGuid, "Duplicate"));
            var root = Path.Combine(Path.GetTempPath(), "wt-add-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root);
                store.Save(session.Snapshot, null);
                Assert.Equal(session.Export().Files.Values.Single(), backend.Export(store.Load(workspace.Id).Workspace.Profile).Files.Values.Single());
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            session.Discard();
            Assert.False(session.HasChanges);
            Assert.Equal(2, session.Snapshot.Profile.Devices.Count);
            Assert.Empty(session.Snapshot.Hardware);
        }

        private static ControllerObservation Observation() => new(
            new(Guid.NewGuid(), "New controller", true, ProductGuid: Guid.Parse("56781234-0000-0000-0000-504944564944")),
            ControllerCapabilityMapper.Map([WarThunderLayoutFixture.Axis("a36d02e0", 0, 0),
                WarThunderLayoutFixture.Button(0), WarThunderLayoutFixture.Button(1), WarThunderLayoutFixture.Hat(0)]));
    }
}
