using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises adapter-owned command edits through real session review, hardware verification and atomic publication.</summary>
    public sealed class SquadronsCommandEditTests
    {
        private const string Slot = "squadrons.joystick.3";
        private const string Throttle = "IncomStarshipInputConcepts.ConceptThrottle";
        private const string Fire = "IncomStarshipInputConcepts.ConceptFire";
        private const string Menu = "IncomStarshipInputConcepts.ConceptMenu";
        #region BEGIN Public
        /// <summary>Whole-axis review is isolated; commit changes both records once and repeat review detects a no-op.</summary>
        [Fact]
        public void Whole_axis_uses_one_verified_native_transaction()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var edit = Edit(session, SquadronsCommandEditKind.AssignControl, Throttle, "JOY_Y", CommandType.Axis);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            Assert.True(session.ReviewNativeEdit(edit).Changed);
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            Assert.Equal(0, notifications);
            session.ApplyNativeEdit(edit);
            Assert.Equal(1, notifications);
            var rows = Rows(session).Where(row => row.Concept == Throttle).ToArray();
            Assert.Equal(2, rows.Length);
            Assert.All(rows, row => { Assert.Equal(2, row.DeviceId); Assert.Equal("JOY_Y", row.Input!.Key); Assert.Equal(123, row.Fields["identifier"]); });
            Assert.Equal(2, rows.Select(row => row.Input!.Direction).Distinct().Count());
            Assert.False(session.ReviewNativeEdit(edit).Changed);
            session.Discard();
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }

        /// <summary>Digital replacement and creation retain unrelated native records and original settings.</summary>
        [Theory]
        [InlineData(Fire, 0, "JOY_BTN2", CommandType.Button)]
        [InlineData(Menu, 1, "JOY_BTN2", CommandType.Button)]
        [InlineData(Fire, 0, "JOY_POV1_U", CommandType.Hat)]
        public void Digital_command_resolves_replacement_or_creation_in_the_adapter(string concept, int added, string key, CommandType type)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var before = Rows(session);
            var edit = Edit(session, SquadronsCommandEditKind.AssignControl, concept, key, type);
            Assert.True(session.ReviewNativeEdit(edit).Valid);
            session.ApplyNativeEdit(edit);
            var after = Rows(session);
            Assert.Equal(before.Count + added, after.Count);
            Assert.Contains(after, row => row.Concept == concept && row.DeviceId == 2 && row.Input?.Key == key);
            foreach (var row in before.Where(row => row.Concept != concept))
                Assert.Equal(JsonSerializer.Serialize(row), JsonSerializer.Serialize(after.Single(item => item.RecordId == row.RecordId)));
        }

        /// <summary>Advanced edits preserve the selected native direction and leave its sibling byte-for-byte unchanged.</summary>
        [Fact]
        public void Explicit_record_edit_preserves_the_other_axis_direction()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var before = Rows(session).Where(row => row.Concept == Throttle).ToArray();
            var selected = before.First();
            var sibling = before.Single(row => row.RecordId != selected.RecordId);
            var edit = Copy(Edit(session, SquadronsCommandEditKind.AssignRecord, Throttle, "JOY_Y", CommandType.Axis), recordId: selected.RecordId);
            session.ApplyNativeEdit(edit);
            var after = Rows(session);
            Assert.Equal(selected.Input!.Direction, after.Single(row => row.RecordId == selected.RecordId).Input!.Direction);
            Assert.Equal("JOY_Y", after.Single(row => row.RecordId == selected.RecordId).Input!.Key);
            Assert.Equal(JsonSerializer.Serialize(sibling), JsonSerializer.Serialize(after.Single(row => row.RecordId == sibling.RecordId)));
        }

        /// <summary>The explicit single-record route handles digital inputs and a first axis direction without synthesizing its sibling.</summary>
        [Theory]
        [InlineData("JOY_BTN2", CommandType.Button)]
        [InlineData("JOY_Y", CommandType.Axis)]
        public void Explicit_record_edit_owns_its_native_input_kind(string key, CommandType type)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var edit = Copy(Edit(session, SquadronsCommandEditKind.AssignRecord, Fire, key, type), recordId: Fire + ".2");
            session.ApplyNativeEdit(edit);
            var row = Assert.Single(Rows(session), row => row.Concept == Fire);
            Assert.Equal(key, row.Input!.Key); Assert.Equal(type, row.Input.Type);
            Assert.Equal(type == CommandType.Axis ? 1 : 0, row.Input.Direction);
        }

        /// <summary>Reversal requires a complete saved pair and reversing twice restores the exact native output.</summary>
        [Fact]
        public void Reverse_uses_saved_axis_evidence_and_is_reversible()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var edit = Edit(session, SquadronsCommandEditKind.ReverseAxis, Throttle, "JOY_Y", CommandType.Axis);
            Assert.False(session.ReviewNativeEdit(edit).Valid);
            session.ApplyNativeEdit(Copy(edit, kind: SquadronsCommandEditKind.AssignControl));
            var before = session.Export().Files[SquadronsProfileBackend.FileName];
            var pair = SquadronsAxisPair.Find(session.Snapshot.Profile, edit.CommandId, Slot, "JOY_Y")!;
            var notifications = 0; session.Changed += (_, _) => notifications++;
            Assert.True(session.ReviewNativeEdit(edit).Valid);
            session.ApplyNativeEdit(edit);
            Assert.Equal(1, notifications);
            Assert.Equal(-1, Rows(session).Single(row => row.RecordId == pair.Positive.RecordId).Input!.Direction);
            Assert.Equal(1, Rows(session).Single(row => row.RecordId == pair.Negative.RecordId).Input!.Direction);
            session.ApplyNativeEdit(edit);
            Assert.Equal(before, session.Export().Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>Hardware evidence is checked again at commit after an otherwise valid review.</summary>
        [Theory]
        [InlineData("disconnected")]
        [InlineData("product")]
        [InlineData("capabilities")]
        [InlineData("unlinked")]
        public void Fresh_evidence_blocks_stale_native_edits(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var device = Device();
            IReadOnlyList<DetectedDcsDevice> observed = [device];
            IReadOnlyList<ControllerObservation> inputs = [new(device, Capabilities())];
            var session = Open(issuer.Policy, device, () => observed, () => inputs);
            var edit = Edit(session, SquadronsCommandEditKind.AssignControl, Throttle, "JOY_Y", CommandType.Axis);
            Assert.True(session.ReviewNativeEdit(edit).Valid);
            switch (failure)
            {
                case "disconnected": observed = []; break;
                case "product": observed = [device with { ProductGuid = Guid.NewGuid() }]; break;
                case "capabilities": inputs = []; break;
                case "unlinked": session.ApplyControllerChange(session.PreviewCapturedDeviceUnlink(Slot)); break;
            }
            var before = JsonSerializer.Serialize(session.Snapshot);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            Assert.False(session.ReviewNativeEdit(edit).Valid);
            Assert.Throws<InvalidOperationException>(() => session.ApplyNativeEdit(edit));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            Assert.Equal(0, notifications);
        }

        /// <summary>Unknown kinds, foreign commands, stale record addresses and invalid target shapes cannot publish edits.</summary>
        [Theory]
        [InlineData("kind")]
        [InlineData("command")]
        [InlineData("record")]
        [InlineData("foreign-record")]
        [InlineData("reverse-button")]
        [InlineData("controller")]
        [InlineData("input")]
        public void Invalid_requests_leave_the_profile_unchanged(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var edit = Copy(Edit(session, SquadronsCommandEditKind.AssignRecord, Fire, "JOY_BTN2", CommandType.Button), recordId: Fire + ".2");
            edit = failure switch
            {
                "kind" => Copy(edit, kind: (SquadronsCommandEditKind)999),
                "command" => Copy(edit, commandId: "unknown"),
                "record" => Copy(edit, recordId: "missing"),
                "foreign-record" => Copy(edit, recordId: Throttle + ".5"),
                "reverse-button" => Copy(edit, kind: SquadronsCommandEditKind.ReverseAxis),
                "controller" => Copy(edit, deviceId: "system.2"),
                _ => Copy(edit, key: "JOY_UNKNOWN")
            };
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.False(session.ReviewNativeEdit(edit).Valid);
            Assert.Throws<InvalidOperationException>(() => session.ApplyNativeEdit(edit));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }

        /// <summary>Read-only sessions cannot bypass entitlement by using the new adapter-native route.</summary>
        [Fact]
        public void Read_only_native_command_review_is_blocked()
        {
            var session = Open(new EditionPolicy());
            var edit = Edit(session, SquadronsCommandEditKind.AssignControl, Fire, "JOY_BTN2", CommandType.Button);
            Assert.False(session.ReviewNativeEdit(edit).Valid);
            Assert.Throws<InvalidOperationException>(() => session.ApplyNativeEdit(edit));
            Assert.False(session.HasChanges);
        }
        /// <summary>A native edit from another adapter or a null edit never enters the profile transformation.</summary>
        [Fact]
        public void Foreign_and_null_operations_are_rejected()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var foreign = new Heimdall.WarThunder.WarThunderNativeEdit(Heimdall.WarThunder.WarThunderEditKind.Clear, "missing");
            Assert.False(session.ReviewNativeEdit(foreign).Valid);
            Assert.Throws<InvalidOperationException>(() => session.ApplyNativeEdit(foreign));
            Assert.Throws<ArgumentNullException>(() => session.ApplyNativeEdit(null!));
            Assert.False(session.HasChanges);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SquadronsCommandEdit Copy(SquadronsCommandEdit edit, SquadronsCommandEditKind? kind = null,
            string? commandId = null, string? deviceId = null, string? key = null, string? recordId = null) =>
            new(kind ?? edit.Kind, commandId ?? edit.CommandId, deviceId ?? edit.LogicalDeviceId, key ?? edit.Key, edit.Type, recordId ?? edit.RecordId);
        private static SquadronsCommandEdit Edit(GameProfileSession session, SquadronsCommandEditKind kind, string concept, string key, CommandType type) =>
            new(kind, session.Catalog.Commands.Single(command => command.Metadata["squadrons.concept"] == concept).Id, Slot, key, type);
        private static IReadOnlyList<SquadronsBindingRow> Rows(GameProfileSession session) => new SquadronsProfileBackend().ReadBindings(session.Snapshot.Profile);
        private static DetectedDcsDevice Device() => new(Guid.NewGuid(), "Command stick", true, ProductGuid: Guid.NewGuid());
        private static ControllerCapabilities Capabilities() => ControllerCapabilityMapper.Map(
        [
            new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis"),
            new(Guid.NewGuid(), 4u, "Button 1"), new(Guid.NewGuid(), 4u | (1u << 8), "Button 2"),
            new(Guid.NewGuid(), 16u, "Hat 1")
        ]);
        private static GameProfileSession Open(EditionPolicy policy, DetectedDcsDevice? device = null,
            Func<IReadOnlyList<DetectedDcsDevice>>? observed = null, Func<IReadOnlyList<ControllerObservation>>? inputs = null)
        {
            device ??= Device();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Command edit", profile, new() { [Slot] = new(null, device) });
            return new(backend, workspace, policy, [], observed ?? (() => [device]), controllerInputs: inputs ?? (() => [new(device, Capabilities())]));
        }
        #endregion END private helpers
    }
}
