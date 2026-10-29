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
    /// <summary>Exercises captured per-record metadata across generic clear, persistence, recovery and reassignment.</summary>
    public sealed class SquadronsTemplateReassignmentTests
    {
        private const string Slot = "squadrons.joystick.10";
        #region BEGIN Public
        /// <summary>Real directional identifiers remain distinct across clear/save/reopen and reassignment to another device and axis.</summary>
        [Theory]
        [InlineData("Pitch")]
        [InlineData("Roll")]
        [InlineData("Yaw")]
        [InlineData("Throttle")]
        [InlineData("ExperimentalAxis")]
        public void Captured_axis_templates_survive_clear_save_reopen_and_reassign(string name)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (backend, profile, source, concept) = Capture(name);
            var original = backend.ReadBindings(profile);
            var pair = original.Where(x => x.Concept == concept && x.Type == 3).ToArray();
            Assert.Equal(2, pair.Length);
            Assert.NotEqual(pair[0].Fields["identifier"], pair[1].Fields["identifier"]);
            var device = Device();
            var session = Session(backend, profile, issuer.Policy, device);
            session.ApplyControllerChange(session.PreviewClearCapturedRecords(pair.Select(x => x.RecordId).ToArray()));
            var cleared = session.Export().Files[SquadronsProfileBackend.FileName];
            Assert.DoesNotContain(backend.ReadBindings(session.Snapshot.Profile), x => x.Concept == concept && x.Type == 3);
            Assert.DoesNotContain("Templates", cleared, StringComparison.Ordinal);
            var root = Path.Combine(Path.GetTempPath(), "squadrons-template-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root);
                var revision = store.Save(session.Snapshot, null); session.AcceptSaved();
                var loaded = store.Load(session.Snapshot.Id).Workspace;
                session = Open(backend, loaded, issuer.Policy, device);
                var command = backend.GetCatalog(session.Snapshot.Profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
                var plan = SquadronsDrawingAssignment.For(backend, session.Snapshot.Profile, command.Id, Slot, "JOY_RY", true);
                var notifications = 0; session.Changed += (_, _) => notifications++;
                session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, Slot, plan.Input.Key);
                Assert.Equal(1, notifications);
                var written = backend.ReadBindings(session.Snapshot.Profile);
                foreach (var row in pair)
                {
                    var restored = written.Single(x => x.RecordId == row.RecordId);
                    Assert.Equal("JOY_RY", restored.Input!.Key);
                    Assert.Equal(row.Input!.Direction, restored.Input.Direction);
                    Assert.Equal(9, restored.DeviceId);
                    Assert.Equal(row.Fields["identifier"], restored.Fields["identifier"]);
                    Assert.Equal(row.Fields["negate"], restored.Fields["negate"]);
                }
                Assert.All(original.ExceptBy(pair.Select(x => x.RecordId), x => x.RecordId), row =>
                    Assert.Contains(written, next => next.RecordId == row.RecordId && row.Fields.All(f => next.Fields[f.Key] == f.Value)));
                Assert.Contains("Future.Setting preserved", session.Export().Files[SquadronsProfileBackend.FileName], StringComparison.Ordinal);
                store.Save(session.Snapshot, revision);
                var again = Open(backend, store.Load(session.Snapshot.Id).Workspace, issuer.Policy, device);
                again.ApplyControllerChange(again.PreviewClearCapturedRecords(pair.Select(x => x.RecordId).ToArray()));
                var nextPlan = SquadronsDrawingAssignment.For(backend, again.Snapshot.Profile, command.Id, Slot, "JOY_RY", true);
                Assert.NotNull(nextPlan.NegativeRecordId);
                session.Discard();
                Assert.Equal(cleared, session.Export().Files[SquadronsProfileBackend.FileName]);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>An older cleared native export can recover authoring evidence without replacing bindings or hardware links.</summary>
        [Fact]
        public void Importing_original_capture_repairs_a_previously_cleared_configuration()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (backend, profile, source, concept) = Capture("Pitch");
            var cleared = backend.ClearCapturedAssignments(profile, "squadrons.joystick.3");
            var native = backend.Export(cleared).Files;
            var old = backend.Import("controls", native);
            var device = Device(); var session = Session(backend, old, issuer.Policy, device);
            var pins = JsonSerializer.Serialize(session.Snapshot.Hardware);
            var command = backend.GetCatalog(old).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
            Assert.Throws<InvalidOperationException>(() => SquadronsDrawingAssignment.For(backend, old, command.Id, Slot, "JOY_RY", true));
            session.ImportAssignmentTemplates(new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            Assert.Equal(native[SquadronsProfileBackend.FileName], session.Export().Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(pins, JsonSerializer.Serialize(session.Snapshot.Hardware));
            var reopened = Open(backend, JsonSerializer.Deserialize<GameWorkspace>(JsonSerializer.Serialize(session.Snapshot))!, issuer.Policy, device);
            var plan = SquadronsDrawingAssignment.For(backend, reopened.Snapshot.Profile, command.Id, Slot, "JOY_RY", true);
            reopened.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, Slot, "JOY_RY");
            Assert.Equal(2, backend.ReadBindings(reopened.Snapshot.Profile).Count(x => x.Concept == concept && x.Type == 3));
        }
        /// <summary>Archived templates do not bypass collision, connection or enumerated input checks.</summary>
        [Fact]
        public void Restoration_rejects_collisions_and_missing_hardware_without_partial_writes()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (backend, profile, _, concept) = Capture("Pitch"); var device = Device();
            var session = Session(backend, profile, issuer.Policy, device);
            var pair = backend.ReadBindings(profile).Where(x => x.Concept == concept && x.Type == 3).ToArray();
            session.ApplyControllerChange(session.PreviewClearCapturedRecords(pair.Select(x => x.RecordId).ToArray()));
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
            var plan = SquadronsDrawingAssignment.For(backend, session.Snapshot.Profile, command.Id, Slot, "JOY_RY", true);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var disconnected = new GameProfileSession(backend, session.Snapshot, issuer.Policy, [], () => []);
            Assert.Throws<InvalidOperationException>(() => disconnected.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, Slot, "JOY_RY"));
            Assert.Equal(before, JsonSerializer.Serialize(disconnected.Snapshot));
            Assert.Throws<InvalidOperationException>(() => backend.RebindAxisPair(session.Snapshot.Profile, plan.RecordId!, plan.NegativeRecordId!, "squadrons.joystick.3", "JOY_X"));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Buttons use the same captured-template path without allocating a guessed native identifier.</summary>
        [Fact]
        public void Cleared_button_can_be_reassigned_after_serialization()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var device = Device(); var session = Session(backend, profile, issuer.Policy, device);
            session.ApplyControllerChange(session.PreviewClearCapturedRecord("IncomStarshipInputConcepts.ConceptFire.2"));
            var workspace = JsonSerializer.Deserialize<GameWorkspace>(JsonSerializer.Serialize(session.Snapshot))!;
            session = Open(backend, workspace, issuer.Policy, device);
            var command = backend.GetCatalog(session.Snapshot.Profile).Commands.Single(x => x.Metadata["squadrons.concept"] == "IncomStarshipInputConcepts.ConceptFire");
            var plan = SquadronsDrawingAssignment.For(backend, session.Snapshot.Profile, command.Id, Slot, "JOY_BTN2", false);
            session.RebindNative(plan.RecordId!, Slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
            var restored = backend.ReadBindings(session.Snapshot.Profile).Single(x => x.RecordId == "IncomStarshipInputConcepts.ConceptFire.2");
            Assert.Equal("JOY_BTN2", restored.Input!.Key);
            Assert.Equal(123, restored.Fields["identifier"]);
        }
        /// <summary>Archive evidence cannot smuggle unrelated native settings into the game export.</summary>
        [Fact]
        public void Malformed_archive_is_rejected_without_writing_export()
        {
            var (backend, profile, source, _) = Capture("Pitch");
            var evidence = System.Text.Json.Nodes.JsonNode.Parse(profile.NativeData["squadrons.v1"].GetRawText())!;
            evidence["Templates"] = source;
            profile.NativeData["squadrons.v1"] = JsonSerializer.SerializeToElement(evidence);
            Assert.Empty(backend.Export(profile).Files);
            Assert.Throws<InvalidOperationException>(() => backend.ReadAssignableBindings(profile));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static (SquadronsProfileBackend Backend, ProfileDocument Profile, string Source, string Concept) Capture(string name)
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Directional_template_capture"));
            if (name == "ExperimentalAxis") source = source.Replace(".ConceptPitch.", ".ConceptExperimentalAxis.", StringComparison.Ordinal);
            var backend = new SquadronsProfileBackend();
            return (backend, backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source }), source, "IncomStarshipInputConcepts.Concept" + name);
        }
        private static DetectedDcsDevice Device() => new(Guid.NewGuid(), "Synthetic restored axis", true, ProductGuid: Guid.NewGuid());
        private static GameProfileSession Session(SquadronsProfileBackend backend, ProfileDocument profile, EditionPolicy policy, DetectedDcsDevice device) =>
            Open(backend, new(1, Guid.NewGuid(), "Restoration", profile, new() { [Slot] = new(null, device) }), policy, device);
        private static GameProfileSession Open(SquadronsProfileBackend backend, GameWorkspace workspace, EditionPolicy policy, DetectedDcsDevice device)
        {
            var caps = ControllerCapabilityMapper.Map([new(Guid.Parse("a36d02f5-c9f3-11cf-bfc7-444553540000"), 2u, "RY axis"), new(Guid.NewGuid(), 4u | (1u << 8), "Button 2")]);
            return new(backend, workspace, policy, [], () => [device], controllerInputs: () => [new(device, caps)]);
        }
        #endregion END private helpers
    }
}
