using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises missing-controller creation, reviewed live identity and native persistence independently of game discovery.</summary>
    public sealed class SquadronsControllerCreationTests
    {
        #region BEGIN Public
        /// <summary>The omitted throttle fills slot ten without importing a binding or disturbing the other nine units.</summary>
        [Theory]
        [InlineData(false, "\r\n")]
        [InlineData(true, "\r\n")]
        [InlineData(true, "\n")]
        public void Reviewed_TQS_addition_preserves_existing_data_and_supports_axis_assignment(bool blankTen, string newline)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend(); var source = Source(blankTen).Replace("\r\n", newline, StringComparison.Ordinal);
            var device = Device(); var workspace = Workspace(backend, source);
            var session = Open(backend, workspace, issuer.Policy, () => [device]);
            Assert.True(session.CanAddNativeControllers);
            var review = session.PreviewAddNativeController(device.PhysicalGuid, "Viper TQS 1");
            Assert.Equal("squadrons.joystick.10", review.ResultDeviceId);
            Assert.Contains(device.PhysicalGuid.ToString(), review.Summary, StringComparison.Ordinal);
            Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            session.ApplyControllerChange(review);
            Assert.Equal(1, notifications);
            Assert.Equal(10, session.Snapshot.Profile.Devices.Count);
            Assert.Equal(device.PhysicalGuid, session.Snapshot.Hardware[review.ResultDeviceId!].Device.PhysicalGuid);
            Assert.Empty(backend.ReadBindings(session.Snapshot.Profile));
            var text = session.Export().Files[SquadronsProfileBackend.FileName];
            Assert.Contains("GstInput.JoystickDevice10 Viper TQS 1" + newline, text, StringComparison.Ordinal);
            foreach (var line in source.Split(newline).Where(x => !x.StartsWith("GstInput.JoystickDevice10 ", StringComparison.Ordinal)))
                Assert.Contains(line, text, StringComparison.Ordinal);
            var plan = SquadronsDrawingAssignment.For(backend, session.Snapshot.Profile,
                "controls/IncomStarshipInputConcepts.ConceptThrottle", review.ResultDeviceId!, "JOY_Y", true);
            session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, review.ResultDeviceId!, "JOY_Y");
            Assert.All(backend.ReadBindings(session.Snapshot.Profile), row => { Assert.Equal(9, row.DeviceId); Assert.Equal("JOY_Y", row.Input!.Key); });
            var root = Path.Combine(Path.GetTempPath(), "squadrons-add-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null);
                var reopened = Open(backend, store.Load(workspace.Id).Workspace, issuer.Policy, () => [device]);
                Assert.Equal(session.Export().Files[SquadronsProfileBackend.FileName], reopened.Export().Files[SquadronsProfileBackend.FileName]);
                reopened.ApplyControllerChange(reopened.PreviewMoveCapturedSlot(review.ResultDeviceId!, "squadrons.joystick.1"));
                Assert.Equal(device.PhysicalGuid, reopened.Snapshot.Hardware["squadrons.joystick.1"].Device.PhysicalGuid);
                Assert.All(backend.ReadBindings(reopened.Snapshot.Profile), row => Assert.Equal(0, row.DeviceId));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            session.Discard(); Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Names cannot inject native settings or silently duplicate an existing game device name.</summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("Viper\nFuture.Setting changed")]
        [InlineData("Viper\rTQS")]
        [InlineData("Viper\0TQS")]
        [InlineData(" Viper TQS")]
        [InlineData("Viper TQS ")]
        [InlineData("existing 1 1")]
        public void Invalid_or_duplicate_names_do_not_change_native_source(string name)
        {
            var backend = new SquadronsProfileBackend(); var source = Source(true);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var error = Record.Exception(() => backend.AddNativeController(profile, name));
            Assert.True(error is ArgumentException or InvalidOperationException);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Null/oversized names and a full ten-slot profile fail before any publication.</summary>
        [Fact]
        public void Null_long_names_and_full_capacity_are_rejected()
        {
            var backend = new SquadronsProfileBackend(); var workspace = Workspace(backend, Source(true));
            Assert.Throws<ArgumentNullException>(() => backend.AddNativeController(workspace.Profile, null!));
            Assert.Throws<InvalidOperationException>(() => backend.AddNativeController(workspace.Profile, new string('x', 257)));
            var full = backend.AddNativeController(workspace.Profile, "Tenth 1");
            Assert.Throws<InvalidOperationException>(() => backend.AddNativeController(full, "Eleventh 1"));
            Assert.Equal(10, full.Devices.Count);
        }
        /// <summary>Blank declarations referenced by native records are reserved rather than inheriting assignments into a new controller.</summary>
        [Fact]
        public void Referenced_blank_slots_are_never_reused()
        {
            var backend = new SquadronsProfileBackend();
            var source = "GstKeyBinding.InputDataVersion 4\nGstInput.JoystickDevice1 \nFuture.Setting retained\n";
            var fields = new Dictionary<string, int> { ["type"] = 3, ["axis"] = 26, ["button"] = 22, ["deviceid"] = 0, ["identifier"] = 123, ["modifier"] = 0, ["altbutton"] = 0, ["negate"] = 0 };
            source += string.Join("", fields.Select(x => "GstKeyBinding.IncomStarshipInputConcepts.ConceptFire.2." + x.Key + " " + x.Value + "\n"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var created = backend.AddNativeController(profile, "Viper TQS 1");
            Assert.Equal("squadrons.joystick.2", Assert.Single(created.Devices).LogicalId);
            Assert.Equal(0, Assert.Single(backend.ReadBindings(created)).DeviceId);
            Assert.Empty(created.Devices[0].Buttons);
            var exported = backend.Export(created).Files[SquadronsProfileBackend.FileName];
            foreach (var line in source.Split('\n')) Assert.Contains(line, exported, StringComparison.Ordinal);
            Assert.True(exported.IndexOf("GstInput.JoystickDevice2 ", StringComparison.Ordinal) < exported.IndexOf("Future.Setting", StringComparison.Ordinal));
        }
        /// <summary>Controller creation preserves a file without a trailing newline.</summary>
        [Fact]
        public void Missing_line_ending_is_separated_before_appending_a_declaration()
        {
            var backend = new SquadronsProfileBackend(); var workspace = Workspace(backend, Source(false).TrimEnd('\r', '\n'));
            var created = backend.AddNativeController(workspace.Profile, "Viper TQS 1");
            var exported = backend.Export(created).Files[SquadronsProfileBackend.FileName];
            Assert.Contains("GstInput.JoystickDevice10 Viper TQS 1\r\n", exported, StringComparison.Ordinal);
            Assert.EndsWith("Future.Setting retained", exported, StringComparison.Ordinal);
        }
        /// <summary>Live controller changes invalidate approval; a stale review never stages a native slot.</summary>
        [Theory]
        [InlineData("disconnected")]
        [InlineData("product")]
        [InlineData("revision")]
        public void Reviewed_addition_revalidates_before_staging(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend(); var device = Device();
            IReadOnlyList<DetectedDcsDevice> observed = [device];
            var session = Open(backend, Workspace(backend, Source(true)), issuer.Policy, () => observed);
            var review = session.PreviewAddNativeController(device.PhysicalGuid, "Viper TQS 1");
            if (failure == "revision") session.Rename("Changed");
            else observed = failure == "product" ? [device with { ProductGuid = Guid.NewGuid() }] : [];
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(review));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Missing/ambiguous identity and already-linked instances cannot be added as another controller.</summary>
        [Theory]
        [InlineData("missing")]
        [InlineData("ambiguous")]
        [InlineData("product")]
        [InlineData("linked")]
        public void Physical_identity_failures_do_not_create_native_slots(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend(); var device = Device();
            if (failure == "product") device = device with { ProductGuid = Guid.Empty };
            IReadOnlyList<DetectedDcsDevice> observed = failure switch { "missing" => [], "ambiguous" => [device, device], _ => [device] };
            var workspace = Workspace(backend, Source(true));
            if (failure == "linked") workspace.Hardware["squadrons.joystick.1"] = new(null, device);
            var session = Open(backend, workspace, issuer.Policy, () => observed);
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.PreviewAddNativeController(device.PhysicalGuid, "Viper TQS 1"));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Optional adapter capabilities and simulator entitlement remain enforced for addition.</summary>
        [Fact]
        public void Free_and_unsupported_backends_cannot_add_native_controllers()
        {
            var backend = new SquadronsProfileBackend(); var device = Device();
            var free = Open(backend, Workspace(backend, Source(true)), new(), () => [device]);
            Assert.Throws<InvalidOperationException>(() => free.PreviewAddNativeController(device.PhysicalGuid, "Viper TQS 1"));
            using var issuer = new OwnerLicenseTestIssuer(); var elite = new EliteDangerousProfileBackend();
            var profile = elite.Import("controls", new Dictionary<string, string> { ["controls.binds"] = "<Root PresetName=\"Test\" MajorVersion=\"4\" MinorVersion=\"0\" />" });
            var session = new GameProfileSession(elite, new(1, Guid.NewGuid(), "Test", profile, new()), issuer.Policy, [], () => [device]);
            Assert.False(session.CanAddNativeControllers);
            Assert.Throws<InvalidOperationException>(() => session.PreviewAddNativeController(device.PhysicalGuid, "Viper TQS 1"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DetectedDcsDevice Device() => new(Guid.NewGuid(), "Viper TQS", true, ProductGuid: Guid.NewGuid());
        private static string Source(bool blankTen) => string.Join("", Enumerable.Range(1, 9).Select(x => "GstInput.JoystickDevice" + x + " Existing " + x + " 1\r\n")) +
            (blankTen ? "GstInput.JoystickDevice10 \r\n" : "") + "Future.Setting retained\r\n";
        private static GameWorkspace Workspace(SquadronsProfileBackend backend, string source) => new(1, Guid.NewGuid(), "Controls",
            backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source }), new());
        private static GameProfileSession Open(SquadronsProfileBackend backend, GameWorkspace workspace, EditionPolicy policy, Func<IReadOnlyList<DetectedDcsDevice>> observed)
        {
            var caps = ControllerCapabilityMapper.Map([new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis")]);
            return new(backend, workspace, policy, [], observed, controllerInputs: () => observed().Select(x => new ControllerObservation(x, caps)).ToArray());
        }
        #endregion END private helpers
    }
}
