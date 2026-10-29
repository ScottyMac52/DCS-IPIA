using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises first assignments with no captured native binding table or user template import.</summary>
    public sealed class SquadronsFreshProfileTests
    {
        private const string Slot = "squadrons.joystick.2";
        #region BEGIN Public
        /// <summary>Distinct flight, hangar and spectator axes can be created from adapter metadata and persisted.</summary>
        [Theory]
        [InlineData("IncomStarshipInputConcepts.ConceptPitch")]
        [InlineData("IncomStarshipInputConcepts.ConceptRoll")]
        [InlineData("IncomStarshipInputConcepts.ConceptYaw")]
        [InlineData("IncomStarshipInputConcepts.ConceptThrottle")]
        [InlineData("IncomStarshipInputConcepts.ConceptCameraPitch")]
        [InlineData("IncomStarshipInputConcepts.ConceptCameraYaw")]
        [InlineData("IncomSoldierInputConcepts.ConceptCameraPitch")]
        [InlineData("IncomDefaultInputConcepts.ConceptSpectatorCameraPitch")]
        [InlineData("IncomDefaultInputConcepts.ConceptSpectatorCameraRoll")]
        public void Fresh_axis_assignment_stages_only_the_requested_pair(string concept)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend(); var source = Fresh();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            Assert.Empty(backend.ReadBindings(profile));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
            Assert.Equal(CommandType.Axis, command.Type);
            var plan = SquadronsDrawingAssignment.For(backend, profile, command.Id, Slot, "JOY_Y", true);
            Assert.Empty(backend.ReadBindings(profile));
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Fresh stick", true, ProductGuid: Guid.NewGuid());
            var caps = ControllerCapabilityMapper.Map([new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis")]);
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Fresh", profile, new() { [Slot] = new(null, device) });
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [device], controllerInputs: () => [new(device, caps)]);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, Slot, plan.Input.Key);
            Assert.Equal(1, notifications);
            var rows = backend.ReadBindings(session.Snapshot.Profile);
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => { Assert.Equal(concept, row.Concept); Assert.Equal(1, row.DeviceId); Assert.Equal("JOY_Y", row.Input!.Key); });
            Assert.Equal(new[] { -1, 1 }, rows.Select(x => x.Input!.Direction).Order().ToArray());
            Assert.NotEqual(rows[0].Fields["identifier"], rows[1].Fields["identifier"]);
            if (concept == "IncomStarshipInputConcepts.ConceptPitch")
            {
                Assert.Equal(1573114849, rows.Single(x => x.Index == 6).Fields["identifier"]);
                Assert.Equal(-743371054, rows.Single(x => x.Index == 7).Fields["identifier"]);
            }
            var text = session.Export().Files[SquadronsProfileBackend.FileName];
            Assert.Contains("Future.Setting preserved", text, StringComparison.Ordinal);
            Assert.Contains("GstKeyBinding.InputDataVersion 4", text, StringComparison.Ordinal);
            var root = Path.Combine(Path.GetTempPath(), "squadrons-fresh-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null);
                Assert.Equal(text, backend.Export(store.Load(workspace.Id).Workspace.Profile).Files[SquadronsProfileBackend.FileName]);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            session.Discard();
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Fresh buttons and POV-backed commands accept digital callouts without guessing identifiers.</summary>
        [Theory]
        [InlineData("IncomStarshipInputConcepts.ConceptFire", "JOY_BTN2")]
        [InlineData("IncomStarshipInputConcepts.ConceptControlEnginePower", "JOY_BTN2")]
        [InlineData("IncomStarshipInputConcepts.ConceptFire", "JOY_BTN_POV1_U")]
        public void Fresh_digital_assignment_uses_the_same_generic_template_path(string concept, string key)
        {
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = Fresh() });
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
            var plan = SquadronsDrawingAssignment.For(backend, profile, command.Id, Slot, key, false);
            Assert.NotNull(plan.RecordId);
            var next = backend.RebindNative(profile, plan.RecordId, Slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
            var row = Assert.Single(backend.ReadBindings(next));
            Assert.Equal(plan.Input, row.Input); Assert.Equal(1, row.DeviceId);
            Assert.Empty(backend.ReadBindings(profile));
        }
        /// <summary>Only identifiable fresh input settings may omit the version; malformed or future tables remain rejected.</summary>
        [Theory]
        [InlineData("Future.Setting 1\n")]
        [InlineData("GstInput.JoystickDevice1 Stick\nGstKeyBinding.InputDataVersion 5\n")]
        [InlineData("GstInput.JoystickDevice1 Stick\nGstKeyBinding.IncomStarshipInputConcepts.ConceptPitch.6.type 3\n")]
        public void Unsupported_or_incomplete_native_tables_do_not_become_fresh_profiles(string source)
        {
            var backend = new SquadronsProfileBackend();
            Assert.Throws<InvalidOperationException>(() => backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source }));
        }
        /// <summary>Factory metadata cannot claim an occupied native address belonging to another source type.</summary>
        [Fact]
        public void Conflicting_native_addresses_fail_without_overwriting_the_capture()
        {
            var backend = new SquadronsProfileBackend(); var concept = "IncomStarshipInputConcepts.ConceptPitch";
            var source = Fresh() + "GstKeyBinding.InputDataVersion 4\n" + string.Join("", new Dictionary<string, int>
            { ["type"] = 2, ["axis"] = 0, ["button"] = 200, ["deviceid"] = -1, ["identifier"] = 12, ["modifier"] = 0, ["altbutton"] = 0, ["negate"] = 0 }
                .Select(x => "GstKeyBinding." + concept + ".6." + x.Key + " " + x.Value + "\n"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var before = JsonSerializer.Serialize(profile);
            Assert.Throws<InvalidOperationException>(() => SquadronsDrawingAssignment.For(backend, profile, "controls/" + concept, Slot, "JOY_Y", true));
            Assert.Equal(before, JsonSerializer.Serialize(profile));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Fresh() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Fresh_profile_synced"));
        #endregion END private helpers
    }
}
