using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>
    /// PR #323 migration baseline: protects observed native evidence, device selection
    /// projections, and draft/save/discard transitions before shared UI extraction.
    /// These tests intentionally exercise the mainline implementation, not future features.
    /// </summary>
    public sealed class SharedWorkspaceBaselineTests
    {
        private static (SquadronsProfileBackend Backend, Heimdall.Domain.MultiSim.ProfileDocument Profile, string Raw) Squadrons()
        {
            var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("baseline", new Dictionary<string, string>
            {
                [SquadronsProfileBackend.FileName] = raw
            });
            return (backend, profile, raw);
        }

        /// <summary>Projection and controller filtering must not change native data or reassign other slots.</summary>
        [Fact]
        public void Viewing_two_captured_slots_does_not_mutate_native_assignments()
        {
            var (backend, profile, raw) = Squadrons();
            var before = JsonSerializer.Serialize(profile);
            var rows = SquadronsWorkspaceProjection.Rows(profile);
            var slotThree = SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "",
                "squadrons.joystick.3", includeUnverified: true);
            var slotFive = SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "",
                "squadrons.joystick.5", includeUnverified: true);
            Assert.Single(slotThree);
            Assert.Equal(2, slotFive.Count);
            Assert.All(slotThree, row => Assert.Equal("squadrons.joystick.3", row.DeviceId));
            Assert.All(slotFive, row => Assert.Equal("squadrons.joystick.5", row.DeviceId));
            Assert.Equal(before, JsonSerializer.Serialize(profile));
            Assert.Equal(raw, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>Native +/- records represent one physical Z axis but retain independent metadata.</summary>
        [Fact]
        public void Captured_positive_and_negative_axis_evidence_is_retained_on_read_and_export()
        {
            var (backend, profile, raw) = Squadrons();
            var entries = backend.ReadBindings(profile)
                .Where(x => x.Concept == "IncomStarshipInputConcepts.ConceptThrottle")
                .OrderBy(x => x.Index).ToArray();
            Assert.Equal(2, entries.Length);
            Assert.All(entries, row => Assert.Equal(4, row.DeviceId));
            Assert.All(entries, row => Assert.Equal("JOY_Z", row.Input?.Key));
            Assert.Equal([-1, 1], entries.Select(x => x.Input!.Direction).Order().ToArray());
            Assert.Equal([0,1], entries.Select(x => x.Fields["negate"]).Order().ToArray());
            Assert.All(entries, row => Assert.Equal(123, row.Fields["identifier"]));
            Assert.Equal(raw, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>Unsaved display changes never alter captured native bytes; discard and save are distinct.</summary>
        [Fact]
        public void Shared_session_draft_and_save_transitions_are_independent_of_native_export()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (backend, profile, raw) = Squadrons();
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Baseline profile", profile, new());
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
            Assert.False(session.HasChanges);
            session.Rename("Staged name");
            Assert.True(session.HasChanges);
            Assert.Equal("Staged name", session.Snapshot.Name);
            Assert.Equal(raw, session.Export().Files[SquadronsProfileBackend.FileName]);
            session.Discard();
            Assert.False(session.HasChanges);
            Assert.Equal("Baseline profile", session.Snapshot.Name);
            session.Rename("Saved name");
            Assert.True(session.HasChanges);
            session.AcceptSaved();
            Assert.False(session.HasChanges);
            session.Rename("Discard this");
            session.Discard();
            Assert.Equal("Saved name", session.Snapshot.Name);
            Assert.False(session.HasChanges);
            Assert.Equal(raw, session.Export().Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>Unverified records remain inspectable without becoming verified authoring inputs.</summary>
        [Fact]
        public void Hardware_verification_filters_are_read_only_projections()
        {
            var (backend, profile, raw) = Squadrons();
            var rows = SquadronsWorkspaceProjection.Rows(profile);
            var reviewed = SquadronsWorkspaceProjection.ReviewInputs(rows,
                new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["squadrons.joystick.5"] = ["JOY_Z"]
                });
            var editable = SquadronsWorkspaceProjection.Filter(reviewed,
                "Flight", "Axes", "", "squadrons.joystick.5", includeUnverified: false);
            Assert.Equal(2, editable.Count);
            Assert.All(editable, row => Assert.Equal("Supported physical input (slot unconfirmed)", row.HardwareStatus));
            var unsupported = SquadronsWorkspaceProjection.Filter(reviewed,
                "All", "All inputs", "", "squadrons.joystick.3", includeUnverified: false);
            Assert.Empty(unsupported);
            Assert.NotEmpty(SquadronsWorkspaceProjection.Filter(reviewed,
                "All", "All inputs", "", "squadrons.joystick.3", includeUnverified: true));
            Assert.Equal(raw, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
    }
}
