using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises native creation and paired-axis editing through the verified session boundary.</summary>
    public sealed class SquadronsNativeSessionTests
    {
        private const string Slot = "squadrons.joystick.3";
        private const string Throttle = "IncomStarshipInputConcepts.ConceptThrottle.";

        #region BEGIN Public
        [Fact]
        public void Verified_axis_pair_is_staged_atomically_and_discard_restores_native_source()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, backend, source) = Open(issuer.Policy, true, true);
            var notifications = 0;
            session.Changed += (_, _) => notifications++;

            session.RebindNativeAxisPair(Throttle + "5", Throttle + "4", Slot, "JOY_Y");

            Assert.True(session.HasChanges);
            Assert.Equal(1, notifications);
            var rows = backend.ReadBindings(session.Snapshot.Profile);
            var positive = rows.Single(x => x.RecordId == Throttle + "5");
            var negative = rows.Single(x => x.RecordId == Throttle + "4");
            Assert.Equal(new SquadronsInput("JOY_Y", CommandType.Axis, 1), positive.Input);
            Assert.Equal(new SquadronsInput("JOY_Y", CommandType.Axis, -1), negative.Input);
            Assert.Equal(2, positive.DeviceId);
            Assert.Equal(2, negative.DeviceId);
            Assert.Equal(123, positive.Fields["identifier"]);
            Assert.Equal(123, negative.Fields["identifier"]);
            Assert.Equal(1, positive.Fields["negate"]);
            Assert.Equal(0, negative.Fields["negate"]);
            var exported = backend.Import("controls", session.Export().Files);
            Assert.Equal(positive.Input, backend.ReadBindings(exported).Single(x => x.RecordId == positive.RecordId).Input);
            session.Discard();
            Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>Axis reversal uses the verified session boundary and can be discarded without touching the saved capture.</summary>
        [Fact]
        public void Verified_reversal_is_atomic_and_discardable()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, backend, source) = Open(issuer.Policy, true, true);
            session.RebindNativeAxisPair(Throttle + "5", Throttle + "4", Slot, "JOY_Y");
            var before = session.Export().Files[SquadronsProfileBackend.FileName];
            var notifications = 0;
            session.Changed += (_, _) => notifications++;
            session.ReverseNativeAxisPair(Throttle + "5", Throttle + "4", Slot, "JOY_Y");
            Assert.Equal(1, notifications);
            Assert.Equal(-1, backend.ReadBindings(session.Snapshot.Profile).Single(x => x.RecordId == Throttle + "5").Input!.Direction);
            session.ReverseNativeAxisPair(Throttle + "4", Throttle + "5", Slot, "JOY_Y");
            Assert.Equal(before, session.Export().Files[SquadronsProfileBackend.FileName]);
            session.Discard();
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Offline or unverified inputs cannot bypass capability checks through the reversal action.</summary>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void Unready_axis_reversal_preserves_the_session(bool connected, bool verified)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, _, source) = Open(issuer.Policy, connected, verified);
            Assert.Throws<InvalidOperationException>(() => session.ReverseNativeAxisPair(Throttle + "5", Throttle + "4", Slot, "JOY_Y"));
            Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        [Fact]
        public void Verified_native_creation_keeps_the_original_keyboard_and_joystick_records()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, backend, source) = Open(issuer.Policy, true, true);
            var original = backend.ReadBindings(session.Snapshot.Profile);
            var notifications = 0;
            session.Changed += (_, _) => notifications++;

            session.AddSquadronsNativeBinding("IncomStarshipInputConcepts.ConceptMenu",
                Slot, "JOY_BTN1", CommandType.Button, 0);

            Assert.True(session.HasChanges);
            Assert.Equal(1, notifications);
            var rows = backend.ReadBindings(session.Snapshot.Profile);
            Assert.Equal(original.Count + 1, rows.Count);
            Assert.All(original, row => Assert.Contains(rows, next =>
                next.RecordId == row.RecordId && next.Fields.Count == row.Fields.Count &&
                row.Fields.All(field => next.Fields.TryGetValue(field.Key, out var value) && value == field.Value)));
            var added = Assert.Single(rows, x => original.All(old => old.RecordId != x.RecordId));
            Assert.Equal("IncomStarshipInputConcepts.ConceptMenu", added.Concept);
            Assert.Equal(2, added.DeviceId);
            Assert.Equal(new SquadronsInput("JOY_BTN1", CommandType.Button, 0), added.Input);
            Assert.Equal(rows.Count, backend.ReadBindings(backend.Import("controls", session.Export().Files)).Count);
            var frozen = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.AddSquadronsNativeBinding(
                added.Concept, Slot, "JOY_BTN1", CommandType.Button, 0));
            Assert.Equal(frozen, JsonSerializer.Serialize(session.Snapshot));
            Assert.Equal(1, notifications);
            session.Discard();
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void Missing_connection_or_input_evidence_rejects_both_workflows_without_changes(bool connected, bool verified)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, _, source) = Open(issuer.Policy, connected, verified);
            var frozen = JsonSerializer.Serialize(session.Snapshot);
            var notifications = 0;
            session.Changed += (_, _) => notifications++;
            Assert.Throws<InvalidOperationException>(() =>
                session.RebindNativeAxisPair(Throttle + "5", Throttle + "4", Slot, "JOY_Y"));
            Assert.Throws<InvalidOperationException>(() => session.AddSquadronsNativeBinding(
                "IncomStarshipInputConcepts.ConceptMenu", Slot, "JOY_BTN1", CommandType.Button, 0));
            Assert.Equal(frozen, JsonSerializer.Serialize(session.Snapshot));
            Assert.False(session.HasChanges);
            Assert.Equal(0, notifications);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }

        [Fact]
        public void Connected_device_cannot_use_unenumerated_controls_or_an_unlinked_slot()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, _, source) = Open(issuer.Policy, true, true);
            Assert.Throws<InvalidOperationException>(() =>
                session.RebindNativeAxisPair(Throttle + "5", Throttle + "4", Slot, "JOY_X"));
            Assert.Throws<InvalidOperationException>(() => session.AddSquadronsNativeBinding(
                "IncomStarshipInputConcepts.ConceptMenu", Slot, "JOY_BTN2", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() =>
                session.RebindNativeAxisPair(Throttle + "5", Throttle + "4", "squadrons.joystick.5", "JOY_Y"));
            Assert.Throws<InvalidOperationException>(() => session.AddSquadronsNativeBinding(
                "IncomStarshipInputConcepts.ConceptMenu", "squadrons.joystick.5", "JOY_BTN1", CommandType.Button, 0));
            Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        #endregion END Public

        #region BEGIN private helpers
        private static (GameProfileSession Session, SquadronsProfileBackend Backend, string Source) Open(
            EditionPolicy policy, bool connected, bool verified)
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Synthetic stick", true, ProductGuid: Guid.NewGuid());
            var capabilities = ControllerCapabilityMapper.Map(
            [
                new ControllerObject(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis"),
                new ControllerObject(Guid.NewGuid(), 4u, "Button 1")
            ]);
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Native session", profile, new()
                { [Slot] = new(null, device) });
            var session = new GameProfileSession(backend, workspace, policy, [],
                () => connected ? [device] : [], controllerInputs: () => verified ? [new(device, capabilities)] : []);
            return (session, backend, source);
        }
        #endregion END private helpers
    }
}
