using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises explicit captured-slot links independently of native assignments and product-name guesses.</summary>
    public sealed class SquadronsDeviceLinkTests
    {
        #region BEGIN Public
        /// <summary>Duplicate products link only to chosen slots; replacing, unlinking, save/reopen and discard preserve native bytes.</summary>
        [Fact]
        public void Reviewed_links_round_trip_without_changing_native_slots_or_bindings()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend(); var workspace = Workspace(backend);
            var first = Device("Viper TQS"); var second = first with { PhysicalGuid = Guid.NewGuid() };
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [first, second]);
            Assert.True(session.CanLinkCapturedDevices);
            var native = session.Export().Files[SquadronsProfileBackend.FileName];
            var review = session.PreviewCapturedDeviceLink("squadrons.joystick.5", first.PhysicalGuid);
            Assert.Contains(first.PhysicalGuid.ToString(), review.Summary, StringComparison.Ordinal);
            Assert.Contains("Native slots and bindings are unchanged", review.Summary, StringComparison.Ordinal);
            Assert.False(session.HasChanges); session.ApplyControllerChange(review);
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("squadrons.joystick.10", second.PhysicalGuid));
            Assert.Equal(first.PhysicalGuid, session.Snapshot.Hardware["squadrons.joystick.5"].Device.PhysicalGuid);
            Assert.Equal(second.PhysicalGuid, session.Snapshot.Hardware["squadrons.joystick.10"].Device.PhysicalGuid);
            session.AcceptSaved();
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("squadrons.joystick.5", first.PhysicalGuid));
            Assert.False(session.HasChanges);
            session.ApplyControllerChange(session.PreviewCapturedDeviceUnlink("squadrons.joystick.10"));
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("squadrons.joystick.5", second.PhysicalGuid));
            Assert.Contains(first.PhysicalGuid.ToString(), review.Summary, StringComparison.Ordinal);
            Assert.Single(session.Snapshot.Hardware); Assert.Equal(native, session.Export().Files[SquadronsProfileBackend.FileName]);
            session.Discard(); Assert.Equal(2, session.Snapshot.Hardware.Count); Assert.False(session.HasChanges);
            var root = Path.Combine(Path.GetTempPath(), "squadrons-links-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null);
                var reopened = new GameProfileSession(backend, store.Load(workspace.Id).Workspace, issuer.Policy, [], () => []);
                Assert.Equal(2, reopened.Snapshot.Hardware.Count);
                Assert.False(GameInputReadiness.Ready(reopened.Snapshot.Hardware["squadrons.joystick.5"], []));
                reopened.ApplyControllerChange(reopened.PreviewCapturedDeviceUnlink("squadrons.joystick.5"));
                Assert.Single(reopened.Snapshot.Hardware); Assert.Equal(native, reopened.Export().Files[SquadronsProfileBackend.FileName]);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>A captured physical link can receive and retain its independent, late-loaded shared drawing identity.</summary>
        [Fact]
        public void Captured_slot_drawing_can_be_selected_after_async_catalog_and_saved()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend();
            var workspace = Workspace(backend);
            var physical = Device("Viper TQS");
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [physical]);
            const string drawingId = "shared-tqs-drawing";
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("squadrons.joystick.5", physical.PhysicalGuid));
            Assert.Null(session.Snapshot.Hardware["squadrons.joystick.5"].HardwareId);
            Assert.Throws<InvalidOperationException>(() => session.ChangeDrawing("squadrons.joystick.5", drawingId));
            session.SetDrawingCatalog([drawingId]);
            session.AcceptSaved();
            session.ChangeDrawing("squadrons.joystick.5", drawingId);
            Assert.Equal(drawingId, session.Snapshot.Hardware["squadrons.joystick.5"].HardwareId);
            Assert.True(session.HasChanges);
            Assert.False(session.Snapshot.Hardware["squadrons.joystick.5"].DrawingHidden);
            session.AcceptSaved();
            var restored = new GameProfileSession(backend, session.Snapshot, issuer.Policy, [drawingId], () => [physical]);
            Assert.Equal(drawingId, restored.Snapshot.Hardware["squadrons.joystick.5"].HardwareId);
            restored.ChangeDrawing("squadrons.joystick.5", null);
            Assert.True(restored.Snapshot.Hardware["squadrons.joystick.5"].DrawingHidden);
            restored.Discard();
            Assert.False(restored.Snapshot.Hardware["squadrons.joystick.5"].DrawingHidden);
        }
        /// <summary>Moving native slots carries exact physical links; clearing is staged and stale changes cannot apply.</summary>
        [Fact]
        public void Reviewed_native_reorder_preserves_links_and_clear_can_be_discarded()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend();
            var first = Device("Stick"); var throttle = Device("Throttle");
            var session = new GameProfileSession(backend, Workspace(backend), issuer.Policy, [], () => [first, throttle]);
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("squadrons.joystick.3", first.PhysicalGuid));
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("squadrons.joystick.5", throttle.PhysicalGuid));
            session.AcceptSaved();
            var original = session.Export().Files[SquadronsProfileBackend.FileName];
            var reorder = session.PreviewMoveCapturedSlot("squadrons.joystick.5", "squadrons.joystick.3");
            Assert.False(session.HasChanges);
            session.ApplyControllerChange(reorder);
            Assert.True(session.HasChanges);
            Assert.Equal(throttle.PhysicalGuid, session.Snapshot.Hardware["squadrons.joystick.1"].Device.PhysicalGuid);
            Assert.Equal(first.PhysicalGuid, session.Snapshot.Hardware["squadrons.joystick.2"].Device.PhysicalGuid);
            Assert.Equal(2, backend.CountCapturedAssignments(session.Snapshot.Profile, "squadrons.joystick.1"));
            Assert.Equal(1, backend.CountCapturedAssignments(session.Snapshot.Profile, "squadrons.joystick.2"));
            var clear = session.PreviewClearCapturedAssignments("squadrons.joystick.1");
            session.ApplyControllerChange(clear);
            Assert.Equal(0, backend.CountCapturedAssignments(session.Snapshot.Profile, "squadrons.joystick.1"));
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(clear));
            session.Discard();
            Assert.False(session.HasChanges);
            Assert.Equal(original, session.Export().Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(throttle.PhysicalGuid, session.Snapshot.Hardware["squadrons.joystick.5"].Device.PhysicalGuid);
        }
        /// <summary>Stale reviews, unplugged/ambiguous/changed devices and links already used by another slot fail atomically.</summary>
        [Fact]
        public void Link_review_revalidates_live_identity_and_workspace_revision()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var first = Device("Identical name"); IReadOnlyList<DetectedDcsDevice> observed = [first];
            var session = new GameProfileSession(backend, Workspace(backend), issuer.Policy, [], () => observed);
            var review = session.PreviewCapturedDeviceLink("squadrons.joystick.5", first.PhysicalGuid);
            var before = JsonSerializer.Serialize(session.Snapshot);
            observed = []; Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(review));
            observed = [first, first]; Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("squadrons.joystick.5", first.PhysicalGuid));
            observed = [first with { ProductGuid = Guid.NewGuid() }]; Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(review));
            observed = [first with { Connected = false }]; Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("squadrons.joystick.5", first.PhysicalGuid));
            observed = [first with { ProductGuid = Guid.Empty }]; Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("squadrons.joystick.5", first.PhysicalGuid));
            observed = [first];
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("missing", first.PhysicalGuid));
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceUnlink("squadrons.joystick.5"));
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("squadrons.joystick.5", Guid.Empty));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            session.ApplyControllerChange(review);
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("squadrons.joystick.10", first.PhysicalGuid));
            var unlink = session.PreviewCapturedDeviceUnlink("squadrons.joystick.5");
            session.Rename("Changed profile"); Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(unlink));
            var other = new GameProfileSession(backend, Workspace(backend), issuer.Policy, [], () => observed);
            Assert.Throws<InvalidOperationException>(() => other.ApplyControllerChange(review));
        }
        /// <summary>Only an explicitly capable adapter and paid session can stage captured-slot links.</summary>
        [Fact]
        public void Free_and_other_adapters_cannot_bypass_the_link_boundary()
        {
            var backend = new SquadronsProfileBackend(); var workspace = Workspace(backend); var device = Device("TQS");
            var free = new GameProfileSession(backend, workspace, new(), [], () => [device]);
            Assert.Throws<InvalidOperationException>(() => free.PreviewCapturedDeviceLink("squadrons.joystick.5", device.PhysicalGuid));
            Assert.Throws<InvalidOperationException>(() => free.PreviewCapturedDeviceUnlink("squadrons.joystick.5"));
            using var issuer = new OwnerLicenseTestIssuer();
            var elite = new EliteDangerousProfileBackend();
            var profile = elite.Import("controls", new Dictionary<string, string> { ["controls.binds"] = "<Root PresetName=\"Test\" MajorVersion=\"4\" MinorVersion=\"0\" />" });
            var session = new GameProfileSession(elite, new(1, Guid.NewGuid(), "Test", profile, new()), issuer.Policy, [], () => [device]);
            Assert.False(session.CanLinkCapturedDevices);
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("missing", device.PhysicalGuid));
            Assert.False(session.HasChanges);
        }
        /// <summary>Persisted links cannot invent identity or assign one physical instance to two captured slots.</summary>
        [Fact]
        public void Malformed_saved_links_are_rejected_before_opening()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var workspace = Workspace(backend); var device = Device("TQS");
            workspace.Hardware["squadrons.joystick.5"] = new(null, device with { ProductGuid = Guid.Empty });
            Assert.Throws<InvalidDataException>(() => new GameProfileSession(backend, workspace, issuer.Policy, [], () => [device]));
            workspace.Hardware["squadrons.joystick.5"] = new(null, device);
            workspace.Hardware["squadrons.joystick.10"] = new(null, device);
            Assert.Throws<InvalidDataException>(() => new GameProfileSession(backend, workspace, issuer.Policy, [], () => [device]));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DetectedDcsDevice Device(string name) => new(Guid.NewGuid(), name, true, ProductGuid: Guid.NewGuid());
        private static GameWorkspace Workspace(SquadronsProfileBackend backend)
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            return new(1, Guid.NewGuid(), "Space Controls", backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source }), new());
        }
        #endregion END private helpers
    }
}
