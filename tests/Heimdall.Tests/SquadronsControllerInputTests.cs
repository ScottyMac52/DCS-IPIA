using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Prevents generic native records and input lists from inventing controls on linked pedals.</summary>
    public sealed class SquadronsControllerInputTests
    {
        #region BEGIN Public
        /// <summary>Axis-only devices expose only their enumerated axes; stale native buttons stay lossless and cannot be assigned.</summary>
        [Fact]
        public void Pedals_cannot_accept_buttons_or_hats_from_generic_native_records()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var pedals = new DetectedDcsDevice(Guid.NewGuid(), "T-Pendular-Rudder", true, ProductGuid: Guid.NewGuid());
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Pedals", profile, new() { ["squadrons.joystick.3"] = new(null, pedals) });
            var caps = ControllerCapabilityMapper.Map([Axis("a36d02e0", 0), Axis("a36d02e1", 1), Axis("a36d02e3", 2)]);
            IReadOnlyList<ControllerObservation> evidence = [new(pedals, caps)];
            IReadOnlyList<DetectedDcsDevice> connected = [pedals];
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => connected, controllerInputs: () => evidence);
            var keys = GameControllerInputs.VerifiedKeys(workspace.Hardware["squadrons.joystick.3"], evidence)!;
            var choices = SquadronsInputCodec.Choices.Where(x => keys.Contains(x.PhysicalKey)).ToArray();
            Assert.Equal(6, choices.Length); Assert.All(choices, x => Assert.Equal(CommandType.Axis, x.Type));
            var reviewed = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(profile), new Dictionary<string, IReadOnlyCollection<string>> { ["squadrons.joystick.3"] = keys });
            Assert.Contains(reviewed, x => x.InputLabel == "Button 1" && x.HardwareStatus == "Not on linked device");
            Assert.Contains(reviewed, x => x.HardwareStatus == "Hardware unverified");
            Assert.Contains(reviewed, x => x.HardwareStatus == "Preserved system input");
            Assert.Empty(SquadronsWorkspaceProjection.Filter(reviewed, "All", "Verified device inputs", "", "all"));
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_BTN1", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_POV1_U", CommandType.Hat, 0));
            Assert.False(session.HasChanges); Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_RZ", CommandType.Axis, 1);
            var valid = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(session.Snapshot.Profile), new Dictionary<string, IReadOnlyCollection<string>> { ["squadrons.joystick.3"] = keys });
            Assert.Single(SquadronsWorkspaceProjection.Filter(valid, "All", "Verified device inputs", "", "all"));
            Assert.True(session.HasChanges); session.AcceptSaved();
            connected = []; Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_X", CommandType.Axis, 1));
            connected = [pedals]; evidence = [new(pedals, null, "Enumeration failed")];
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_X", CommandType.Axis, 1));
            Assert.False(session.HasChanges);
        }
        /// <summary>Unknown, duplicated, disconnected, changed-product and failed capability evidence never becomes an unrestricted input list.</summary>
        [Fact]
        public void Capability_evidence_must_match_one_connected_linked_instance()
        {
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Pedals", true, ProductGuid: Guid.NewGuid());
            var pin = new GameHardwarePin(null, device); var caps = ControllerCapabilityMapper.Map([Axis("a36d02e0", 0)]);
            var valid = new ControllerObservation(device, caps);
            Assert.Equal("JOY_X", Assert.Single(GameControllerInputs.VerifiedKeys(pin, [valid])!));
            Assert.Null(GameControllerInputs.VerifiedKeys(null, [valid]));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, []));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, [valid, valid]));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, [valid with { Device = device with { Connected = false } }]));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, [valid with { Device = device with { ProductGuid = Guid.NewGuid() } }]));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, [valid with { Error = "Unavailable" }]));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, [valid with { Capabilities = null }]));
            Assert.Null(GameControllerInputs.VerifiedKeys(pin, [valid with { Capabilities = caps with { SchemaVersion = 2 } }]));
            Assert.Null(GameControllerInputs.VerifiedKeys(new(null, device with { ProductGuid = Guid.Empty }), [valid]));
            Assert.Empty(GameControllerInputs.VerifiedKeys(pin, [valid with { Capabilities = ControllerCapabilityMapper.Map([]) }])!);
            Assert.Equal("JOY_BTN_POV1_U", new SquadronsInput("JOY_POV1_U", CommandType.Hat, 0).PhysicalKey);
        }
        /// <summary>Actual hats use the shared POV spelling; unlinked slots and unknown native encodings remain distinct from verified controls.</summary>
        [Fact]
        public void Real_hats_are_assignable_only_to_the_enumerated_linked_stick()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var stick = new DetectedDcsDevice(Guid.NewGuid(), "Stick", true, ProductGuid: Guid.NewGuid());
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Stick", profile, new() { ["squadrons.joystick.3"] = new(null, stick) });
            var caps = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 16u, "Hat 1")]);
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [stick], controllerInputs: () => [new(stick, caps)]);
            session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_POV1_U", CommandType.Hat, 0);
            Assert.Contains(backend.ReadBindings(session.Snapshot.Profile), x => x.Input is { Key: "JOY_POV1_U" });
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.5", "JOY_POV1_U", CommandType.Hat, 0));
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_POV2_U", CommandType.Hat, 0));
            var unverified = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [stick]);
            Assert.Throws<InvalidOperationException>(() => unverified.RebindNative("IncomStarshipInputConcepts.ConceptFire.2", "squadrons.joystick.3", "JOY_POV1_U", CommandType.Hat, 0));
            var unknown = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source.Replace("ConceptFire.2.button 22", "ConceptFire.2.button 174", StringComparison.Ordinal) });
            Assert.Contains(SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(unknown), new Dictionary<string, IReadOnlyCollection<string>>()), x => x.HardwareStatus == "Preserved native encoding");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ControllerObject Axis(string prefix, uint instance) => new(Guid.Parse(prefix + "-c9f3-11cf-bfc7-444553540000"), 2u | (instance << 8), "Axis");
        #endregion END private helpers
    }
}
