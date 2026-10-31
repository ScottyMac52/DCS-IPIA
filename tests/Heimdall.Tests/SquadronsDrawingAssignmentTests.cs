using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Characterizes native drawing plans and their verified, isolated session execution.</summary>
    public sealed class SquadronsDrawingAssignmentTests
    {
        private const string Slot = "squadrons.joystick.3";
        private const string Throttle = "IncomStarshipInputConcepts.ConceptThrottle";
        private const string Fire = "IncomStarshipInputConcepts.ConceptFire";
        private const string Menu = "IncomStarshipInputConcepts.ConceptMenu";
        #region BEGIN Public
        /// <summary>Both directional records move in one session notification and discard restores the exact source.</summary>
        [Fact]
        public void Whole_axis_plan_stages_both_directions_and_preserves_native_settings()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (backend, profile, source) = Open();
            var plan = Plan(backend, profile, Throttle, "JOY_Y", true);
            Assert.Equal(Throttle + ".5", plan.RecordId);
            Assert.Equal(Throttle + ".4", plan.NegativeRecordId);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Stick", true, ProductGuid: Guid.NewGuid());
            var caps = ControllerCapabilityMapper.Map([new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis")]);
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Drawing plan", profile, new() { [Slot] = new(null, device) });
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [device], controllerInputs: () => [new(device, caps)]);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, Slot, plan.Input.Key);
            Assert.Equal(1, notifications);
            var rows = backend.ReadBindings(session.Snapshot.Profile);
            Assert.Equal(1, rows.Single(x => x.RecordId == plan.RecordId).Fields["negate"]);
            Assert.Equal(0, rows.Single(x => x.RecordId == plan.NegativeRecordId).Fields["negate"]);
            Assert.All(rows.Where(x => x.Concept == Throttle), row =>
            {
                Assert.Equal("JOY_Y", row.Input!.Key);
                Assert.Equal(2, row.DeviceId);
                Assert.Equal(123, row.Fields["identifier"]);
            });
            session.Discard();
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Existing buttons resolve a replacement while keyboard-only concepts request verified creation.</summary>
        [Theory]
        [InlineData(Fire, Fire + ".2")]
        [InlineData(Menu, null)]
        public void Button_plan_preserves_or_requests_native_record(string concept, string? record)
        {
            var (backend, profile, source) = Open();
            var plan = Plan(backend, profile, concept, "JOY_BTN2", false);
            Assert.Equal(record, plan.RecordId);
            Assert.Null(plan.NegativeRecordId);
            Assert.Equal(concept, plan.Concept);
            Assert.Equal(new SquadronsInput("JOY_BTN2", CommandType.Button, 0), plan.Input);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>A missing half axis cannot become a partial draft.</summary>
        [Theory]
        [InlineData("4")]
        [InlineData("5")]
        public void Incomplete_axis_rejects_the_entire_plan(string removed)
        {
            var (backend, _, source) = Open();
            source = string.Join('\n', source.Split('\n').Where(x => !x.StartsWith("GstKeyBinding." + Throttle + "." + removed + ".", StringComparison.Ordinal)));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            Assert.Throws<InvalidOperationException>(() => Plan(backend, profile, Throttle, "JOY_Y", true));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Read-only, nonexistent and malformed native source identities cannot receive a drawing assignment.</summary>
        [Theory]
        [InlineData("all")]
        [InlineData("system.2")]
        [InlineData("squadrons.joystick.0")]
        [InlineData("squadrons.joystick.11")]
        [InlineData("squadrons.joystick.bad")]
        [InlineData("squadrons.joystick.1")]
        public void Invalid_slot_rejects_without_native_mutation(string slot)
        {
            var (backend, profile, source) = Open();
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == Fire);
            Assert.Throws<InvalidOperationException>(() => SquadronsDrawingAssignment.For(backend, profile, command.Id, slot, "JOY_BTN2", false));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Unknown controls, commands and null dependencies fail before staging anything.</summary>
        [Fact]
        public void Invalid_command_or_input_cannot_produce_a_native_plan()
        {
            var (backend, profile, source) = Open();
            Assert.Throws<InvalidOperationException>(() => Plan(backend, profile, Fire, "JOY_UNKNOWN", false));
            Assert.Throws<InvalidOperationException>(() => SquadronsDrawingAssignment.For(backend, profile, "unknown", Slot, "JOY_BTN2", false));
            Assert.Throws<ArgumentNullException>(() => SquadronsDrawingAssignment.For(null!, profile, "unknown", Slot, "JOY_BTN2", false));
            Assert.Throws<ArgumentNullException>(() => SquadronsDrawingAssignment.For(backend, null!, "unknown", Slot, "JOY_BTN2", false));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SquadronsDrawingAssignment Plan(SquadronsProfileBackend backend, ProfileDocument profile, string concept, string key, bool axis) =>
            SquadronsDrawingAssignment.For(backend, profile, backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept).Id, Slot, key, axis);
        private static (SquadronsProfileBackend Backend, ProfileDocument Profile, string Source) Open()
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var backend = new SquadronsProfileBackend();
            return (backend, backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source }), source);
        }
        #endregion END private helpers
    }
}
