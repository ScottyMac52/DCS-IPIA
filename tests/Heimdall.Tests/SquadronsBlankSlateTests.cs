using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Enforces authoring of the entire known catalog from declared devices and zero active bindings.</summary>
    public sealed class SquadronsBlankSlateTests
    {
        private const string Slot = "squadrons.joystick.2";
        #region BEGIN Public
        /// <summary>Every advertised known command resolves and stages without game-side preparation or template import.</summary>
        [Fact]
        public void Every_known_command_can_be_assigned_from_an_empty_binding_table()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new SquadronsProfileBackend();
            var source = Fresh();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var commands = backend.GetCatalog(profile).Commands;
            Assert.Equal(125, commands.Count);
            Assert.Empty(backend.ReadBindings(profile));
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Blank slate stick", true, ProductGuid: Guid.NewGuid());
            var capabilities = ControllerCapabilityMapper.Map([
                new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis"),
                new(Guid.NewGuid(), 4u | (4u << 8), "Button 5")]);
            foreach (var command in commands)
            {
                Assert.NotEqual(CommandType.Other, command.Type);
                var axis = command.Type == CommandType.Axis;
                var key = axis ? "JOY_Y" : "JOY_BTN5";
                var plan = SquadronsDrawingAssignment.For(backend, profile, command.Id, Slot, key, axis);
                var workspace = new GameWorkspace(1, Guid.NewGuid(), "Blank slate", profile, new() { [Slot] = new(null, device) });
                var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => [device],
                    controllerInputs: () => [new(device, capabilities)]);
                if (axis)
                    session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, Slot, plan.Input.Key);
                else
                    session.RebindNative(plan.RecordId!, Slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
                var rows = backend.ReadBindings(session.Snapshot.Profile);
                Assert.Equal(axis ? 2 : 1, rows.Count);
                Assert.All(rows, row =>
                {
                    Assert.Equal(command.Metadata["squadrons.concept"], row.Concept);
                    Assert.Equal(1, row.DeviceId);
                    Assert.Equal(key, row.Input!.Key);
                });
                var exported = session.Export().Files[SquadronsProfileBackend.FileName];
                var reopened = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = exported });
                Assert.Equal(exported, backend.Export(reopened).Files[SquadronsProfileBackend.FileName]);
                session.Discard();
                Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            }
            Assert.Empty(backend.ReadBindings(profile));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>The video commands use native custom records, persist, clear and reassign without importing a prepared profile.</summary>
        [Theory]
        [InlineData("ShieldFront")]
        [InlineData("ShieldBack")]
        [InlineData("ShieldBalance")]
        public void Fresh_shield_binding_preserves_the_observed_custom_record_and_reopens(string name)
        {
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = Fresh() });
            var concept = "IncomStarshipInputConcepts.Concept" + name;
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
            var plan = SquadronsDrawingAssignment.For(backend, profile, command.Id, Slot, "JOY_BTN5", false);
            var assigned = backend.RebindNative(profile, plan.RecordId!, Slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
            var row = Assert.Single(backend.ReadBindings(assigned));
            Assert.Equal(concept + ".1", row.RecordId);
            Assert.Equal(0, row.Fields["identifier"]);
            Assert.Equal(0, row.Fields["negate"]);
            Assert.Equal(0, row.Fields["modifier"]);
            Assert.Equal(0, row.Fields["altbutton"]);
            Assert.Equal(new SquadronsInput("JOY_BTN5", CommandType.Button, 0), row.Input);
            var root = Path.Combine(Path.GetTempPath(), "squadrons-blank-slate-" + Guid.NewGuid().ToString("N"));
            try
            {
                var workspace = new GameWorkspace(1, Guid.NewGuid(), "Blank slate shield", assigned, new());
                var store = new FileGameWorkspaceStore(root);
                store.Save(workspace, null);
                var reopened = store.Load(workspace.Id).Workspace.Profile;
                var cleared = backend.ClearCapturedRecord(reopened, row.RecordId);
                Assert.Empty(backend.ReadBindings(cleared));
                var restored = backend.RebindNative(cleared, row.RecordId, Slot, "JOY_BTN2", CommandType.Button, 0);
                Assert.Equal("JOY_BTN2", Assert.Single(backend.ReadBindings(restored)).Input!.Key);
                Assert.Empty(backend.ReadBindings(profile));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>A custom descriptor appends after a system record at its usual address instead of hiding the command or replacing data.</summary>
        [Fact]
        public void Custom_command_creation_preserves_an_occupied_system_address()
        {
            var backend = new SquadronsProfileBackend();
            const string concept = "IncomStarshipInputConcepts.ConceptShieldFront";
            var source = Fresh() + "GstKeyBinding.InputDataVersion 4\n" +
                string.Join("", new Dictionary<string, int>
                {
                    ["altbutton"] = 0, ["axis"] = 0, ["button"] = 57, ["deviceid"] = -1,
                    ["identifier"] = 123, ["modifier"] = 255, ["negate"] = 0, ["type"] = 2
                }.Select(field => "GstKeyBinding." + concept + ".1." + field.Key + " " + field.Value + "\n"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"] == concept);
            var plan = SquadronsDrawingAssignment.For(backend, profile, command.Id, Slot, "JOY_BTN5", false);
            Assert.Equal(concept + ".2", plan.RecordId);
            var next = backend.RebindNative(profile, plan.RecordId!, Slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
            var rows = backend.ReadBindings(next);
            Assert.Equal(2, rows.Count);
            Assert.Equal(123, rows.Single(x => x.RecordId == concept + ".1").Fields["identifier"]);
            Assert.Equal(2, rows.Single(x => x.RecordId == concept + ".1").Type);
            Assert.Equal(0, rows.Single(x => x.RecordId == concept + ".2").Fields["identifier"]);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Fresh() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Fresh_profile_synced"));
        #endregion END private helpers
    }
}
