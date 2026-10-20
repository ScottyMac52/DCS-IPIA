using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Games;
using Dcs.Ipia.Domain.MultiSim;
using Dcs.Ipia.EliteDangerous;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.TestSupport;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies new ED references, optional artwork, exact physical choices and unchanged edition boundaries.</summary>
    public sealed class EliteControllerAdditionTests
    {
        #region BEGIN Public
        /// <summary>An unrepresented HID controller can be saved without artwork, assigned and exported/reimported.</summary>
        [Fact]
        public void New_controller_without_drawing_round_trips_and_discard_restores_membership()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new EliteDangerousProfileBackend(); var observed = Device();
            var session = Session(Workspace(), issuer.Policy, [observed]);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var id = session.ConnectController(observed.PhysicalGuid);
            Assert.True(session.HasChanges); Assert.Null(session.Snapshot.Hardware[id].HardwareId);
            Assert.Equal(session.Snapshot.Profile.NativeData["ed.v1"].GetProperty("Sources").GetProperty("Add.4.2.binds").GetString(), session.Export().Files.Values.Single());
            Assert.Equal("06A30BAC", session.Snapshot.Profile.Devices.Single(x => x.LogicalId == id).Identity!.SimSpecificIds["ed.device"]);
            Assert.Equal(id, session.ConnectController(observed.PhysicalGuid));
            var command = session.Catalog.Commands.Single().Id;
            session.AssignButton(id, command, "Primary", "Joy_3", BindingMode.Hold, []);
            var root = Path.Combine(Path.GetTempPath(), "ipia-add-controller-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null);
                var reopened = Session(store.Load(session.Snapshot.Id).Workspace, issuer.Policy, []);
                var native = backend.Import("controls", reopened.Export().Files);
                var added = native.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "06A30BAC");
                Assert.Equal("Joy_3", Assert.Single(added.Buttons).SourceButton);
                Assert.Equal(BindingMode.Hold, added.Buttons[0].Mode);
                Assert.Contains("<FutureSetting Value=\"preserve\"", reopened.Export().Files.Values.Single());
                GameWorkspaceContract.Validate(reopened.Snapshot);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            session.Discard(); Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Unique captured aliases are linked rather than duplicated, while drawings can be changed independently.</summary>
        [Fact]
        public void Existing_native_reference_is_reused_and_artwork_is_optional()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var observed = Device("0402044f-0000-0000-0000-504944564944");
            var session = Session(Workspace("ThrustMasterWarthogJoystick"), issuer.Policy, [observed]);
            var original = session.Snapshot.Profile.Devices.Single().LogicalId;
            Assert.Equal(original, session.ConnectController(observed.PhysicalGuid));
            Assert.Single(session.Snapshot.Profile.Devices);
            session.ChangeDrawing(original, "tm-mfd"); Assert.Equal("tm-mfd", session.Snapshot.Hardware[original].HardwareId);
            session.ChangeDrawing(original, null); Assert.Null(session.Snapshot.Hardware[original].HardwareId);
            Assert.Equal("ThrustMasterWarthogJoystick", session.Snapshot.Profile.Devices.Single().Identity!.SimSpecificIds["ed.device"]);
            Assert.Throws<InvalidOperationException>(() => session.ChangeDrawing(original, "unknown"));
        }
        /// <summary>Repeated products need captured ED indices and an explicit reference choice, never an invented ordinal.</summary>
        [Fact]
        public void Identical_controllers_require_captured_indices_and_do_not_overwrite_another_unit()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var a = Device(); var b = a with { PhysicalGuid = Guid.NewGuid() };
            var session = Session(Workspace(), issuer.Policy, [a, b]);
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.ConnectController(a.PhysicalGuid));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            var source = "<Root PresetName='Indices' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='06A30BAC' DeviceIndex='0' Key='Joy_1'/><Secondary Device='06A30BAC' DeviceIndex='1' Key='Joy_2'/></PrimaryFire></Root>";
            session = Session(FromSource(source), issuer.Policy, [a, b]);
            Assert.Equal(2, session.ControllerReferences(a.PhysicalGuid).Count);
            Assert.Throws<InvalidOperationException>(() => session.ConnectController(a.PhysicalGuid));
            var ids = session.Snapshot.Profile.Devices.Select(x => x.LogicalId).ToArray();
            session.ConnectController(a.PhysicalGuid, logicalId: ids[0]);
            Assert.Throws<InvalidOperationException>(() => session.ConnectController(b.PhysicalGuid, logicalId: ids[0]));
            session.ConnectController(b.PhysicalGuid);
            Assert.Equal(b.PhysicalGuid, session.Snapshot.Hardware[ids[1]].Device.PhysicalGuid);
            Assert.Equal(source, session.Export().Files.Values.Single());
            var unindexed = Session(Workspace("06A30BAC"), issuer.Policy, [a, b]);
            Assert.Throws<InvalidOperationException>(() => unindexed.ConnectController(a.PhysicalGuid));
        }
        /// <summary>Adding a controller or choosing a drawing cannot grant Free access to another manufacturer.</summary>
        [Fact]
        public void Free_access_still_requires_native_manufacturer_and_reviewed_catalog_evidence()
        {
            var other = Device(); var free = Session(Workspace(), new(), [other]);
            Assert.Throws<InvalidOperationException>(() => free.ConnectController(other.PhysicalGuid, "tm-mfd"));
            Assert.Empty(free.Snapshot.Hardware);
            var tm = Device("1001044f-0000-0000-0000-504944564944");
            free = Session(Workspace(), new(), [tm]);
            var id = free.ConnectController(tm.PhysicalGuid);
            Assert.NotEmpty(free.AccessMessage);
            Assert.Throws<InvalidOperationException>(() => free.Export());
            free.ChangeDrawing(id, "tm-mfd"); Assert.Empty(free.AccessMessage); Assert.True(free.Export().Succeeded);
            var changedProduct = tm with { ProductGuid = Guid.Parse("1002044f-0000-0000-0000-504944564944") };
            Assert.NotEmpty(Session(free.Snapshot, new(), [changedProduct]).AccessMessage);
            free.ChangeDrawing(id, null); Assert.NotEmpty(free.AccessMessage);
            Assert.Throws<InvalidOperationException>(() => free.ChangeDrawing(id, "moza-ab9"));
        }
        /// <summary>Unsupported identity mutations and bad observations fail before publishing any state.</summary>
        [Theory]
        [InlineData("missing")]
        [InlineData("ambiguous")]
        [InlineData("nonhid")]
        [InlineData("drawing")]
        [InlineData("reference")]
        public void Invalid_choices_are_atomic(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer(); var device = Device();
            var observed = failure switch { "missing" => Array.Empty<DetectedDcsDevice>(), "ambiguous" => new[] { device, device }, "nonhid" => new[] { device with { ProductGuid = Guid.Empty } }, _ => new[] { device } };
            var session = Session(Workspace(), issuer.Policy, observed); var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.ConnectController(device.PhysicalGuid, failure == "drawing" ? "unknown" : null, failure == "reference" ? "missing" : null));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Native additions preserve captured devices and reject unknown indices or forged identities.</summary>
        [Theory]
        [InlineData("index")]
        [InlineData("identity")]
        [InlineData("remove")]
        [InlineData("duplicate")]
        public void Backend_rejects_unsafe_membership_changes(string failure)
        {
            var backend = new EliteDangerousProfileBackend();
            var original = Workspace().Profile;
            var added = backend.AddDevice(original, Device());
            Assert.Single(original.Devices);
            var newDevice = added.Devices.Last();
            switch (failure)
            {
                case "index": newDevice.Identity!.SimSpecificIds["ed.deviceIndex"] = "0"; break;
                case "identity": newDevice.Identity!.InstanceId = Guid.NewGuid().ToString(); break;
                case "remove": added.Devices.RemoveAt(0); break;
                default: added.Devices.Add(newDevice); break;
            }
            Assert.False(backend.Export(added).Succeeded);
        }
        /// <summary>Axis and cross-device modifier output can target a newly added controller without rebasing captured sources.</summary>
        [Fact]
        public void New_controller_axes_and_chords_export_through_the_same_native_writer()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var observed = Device();
            var profile = Workspace().Profile;
            var source = profile.NativeData["ed.v1"].GetProperty("Sources").GetProperty("Add.4.2.binds").GetString()!;
            source = source.Replace("</Root>", "<PitchAxisRaw><Binding Device='{NoDevice}' Key=''/><Inverted Value='0'/><Deadzone Value='0'/></PitchAxisRaw></Root>", StringComparison.Ordinal);
            var session = Session(FromSource(source), issuer.Policy, [observed]);
            var id = session.ConnectController(observed.PhysicalGuid);
            var keyboard = session.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard").LogicalId;
            var commands = session.Catalog.Commands;
            session.AssignAxis(id, commands.Single(x => x.Type == CommandType.Axis).Id, "Joy_XAxis", true, 8);
            session.AssignButton(id, commands.Single(x => x.Type == CommandType.Button).Id, "Secondary", "Joy_5", BindingMode.Press, [new() { Source = keyboard, Value = "Key_LeftShift" }]);
            var native = new EliteDangerousProfileBackend().Import("controls", session.Export().Files);
            var controller = native.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "06A30BAC");
            Assert.True(Assert.Single(controller.Axes).Invert); Assert.Equal(8, controller.Axes[0].Deadzone);
            Assert.Equal("Key_LeftShift", Assert.Single(Assert.Single(controller.Buttons).Modifiers).Value);
        }
        /// <summary>Automatic artwork suggestions require a unique exact catalog name and never choose among alternate views.</summary>
        [Fact]
        public void Artwork_suggestions_leave_unknown_and_ambiguous_devices_unselected()
        {
            var device = Device();
            var drawing = new WorkspaceHardware("one", "First", device.FileName, new("Controller", null, "test.svg", 20, 20, [], []));
            Assert.Equal("one", GameControllerDrawings.Suggest([drawing], device));
            Assert.Null(GameControllerDrawings.Suggest([drawing, drawing with { DeviceId = "two" }], device));
            Assert.Null(GameControllerDrawings.Suggest([drawing], device with { Name = "Unknown grip" }));
            Assert.Null(GameControllerDrawings.Suggest([drawing with { DefaultFileName = "not-native" }], device));
        }
        /// <summary>A controller needs no throwaway native binding to remain in a saved IPIA profile.</summary>
        [Fact]
        public void Empty_added_controller_survives_store_reopen_without_changing_native_source()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var observed = Device();
            var session = Session(Workspace(), issuer.Policy, [observed]);
            var native = session.Export().Files.Values.Single();
            var id = session.ConnectController(observed.PhysicalGuid);
            var root = Path.Combine(Path.GetTempPath(), "ipia-empty-controller-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null);
                var reopened = Session(store.Load(session.Snapshot.Id).Workspace, issuer.Policy, []);
                Assert.Empty(reopened.Snapshot.Profile.Devices.Single(x => x.LogicalId == id).Buttons);
                Assert.Null(reopened.Snapshot.Hardware[id].HardwareId);
                Assert.Equal(native, reopened.Export().Files.Values.Single());
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DetectedDcsDevice Device(string product = "0bac06a3-0000-0000-0000-504944564944") => new(Guid.NewGuid(), "Controller", true, ProductGuid: Guid.Parse(product));
        private static GameWorkspace Workspace(string token = "Keyboard") => FromSource($"<Root PresetName='Add' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='{token}' Key='Key_Space'/><Secondary Device='{{NoDevice}}' Key=''/></PrimaryFire><FutureSetting Value='preserve'/></Root>");
        private static GameWorkspace FromSource(string source) => new(1, Guid.NewGuid(), "Controllers", new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Add.4.2.binds"] = source }), new());
        private static GameProfileSession Session(GameWorkspace workspace, EditionPolicy policy, IReadOnlyList<DetectedDcsDevice> observed) => new(new EliteDangerousProfileBackend(), workspace, policy, ["tm-mfd"], () => observed, new EliteDeviceReferenceMatcher());
        #endregion END private helpers
    }
}
