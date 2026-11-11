using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>All native-profile games reuse only unchanged comparison output; candidates and edits remain independently validated.</summary>
    public sealed class GameReviewBaselineTests
    {
        #region BEGIN Public
        /// <summary>Repeated drafts share one baseline, while content, save, discard and session changes invalidate it.</summary>
        [Theory]
        [InlineData("elite")][InlineData("squadrons")][InlineData("war-thunder")]
        public void Baseline_is_shared_but_candidate_evaluation_stays_live(string game)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(game, issuer.Policy); var edit = new ReviewNativeEditProbe(session.Snapshot.Profile.BackendId);
            var before = session.ContentFingerprint; var notifications = 0;
            session.Changed += (_, _) => notifications++;
            for (var i = 0; i < 20; i++) Assert.Equal(new(true, false, ""), session.ReviewNativeEdit(edit));
            Assert.Equal(20, edit.Calls); Assert.Equal(1, session.ReviewBaselineBuilds);
            Assert.Equal(before, session.ContentFingerprint); Assert.Equal(0, notifications); Assert.False(session.HasChanges);
            edit.Fail = true;
            Assert.False(session.ReviewNativeEdit(edit).Valid); Assert.Equal(21, edit.Calls); Assert.Equal(1, session.ReviewBaselineBuilds);
            edit.Fail = false;
            session.Rename("Renamed"); Assert.True(session.ReviewNativeEdit(edit).Valid); Assert.Equal(2, session.ReviewBaselineBuilds);
            session.AcceptSaved(); Assert.True(session.ReviewNativeEdit(edit).Valid); Assert.Equal(3, session.ReviewBaselineBuilds);
            session.Rename("Again"); Assert.True(session.ReviewNativeEdit(edit).Valid); Assert.Equal(4, session.ReviewBaselineBuilds);
            session.Discard(); Assert.True(session.ReviewNativeEdit(edit).Valid); Assert.Equal(5, session.ReviewBaselineBuilds);
            Assert.Equal("Renamed", session.Name); Assert.False(session.HasChanges);
            var reopened = Open(game, issuer.Policy); Assert.True(reopened.ReviewNativeEdit(edit).Valid);
            Assert.Equal(1, reopened.ReviewBaselineBuilds);
        }
        /// <summary>Failed candidates and denied access never populate a comparison baseline.</summary>
        [Theory]
        [InlineData("elite")][InlineData("squadrons")][InlineData("war-thunder")]
        public void Failures_do_not_install_a_baseline_or_bypass_access(string game)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(game, issuer.Policy); var edit = new ReviewNativeEditProbe(session.Snapshot.Profile.BackendId) { Fail = true };
            Assert.False(session.ReviewNativeEdit(edit).Valid); Assert.Equal(0, session.ReviewBaselineBuilds);
            edit.Fail = false; Assert.True(session.ReviewNativeEdit(edit).Valid); Assert.Equal(1, session.ReviewBaselineBuilds);
            var locked = Open(game, new EditionPolicy()); var calls = edit.Calls;
            Assert.False(locked.ReviewNativeEdit(edit).Valid); Assert.Equal(calls, edit.Calls); Assert.Equal(0, locked.ReviewBaselineBuilds);
        }
        /// <summary>Identical display drafts share one snapshot, including separately constructed modifier inputs.</summary>
        [Fact]
        public void Assignment_availability_is_bounded_and_fresh_review_stays_independent()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open("elite", issuer.Policy);
            var draft = AssignmentDraft(session);
            var copies = session.SnapshotCopies;
            var expected = session.ReviewAssignmentAvailability(draft);
            Assert.True(expected.Valid && expected.Changed);
            for (var i = 0; i < 20; i++)
                Assert.Equal(expected, session.ReviewAssignmentAvailability(draft with
                {
                    Modifiers = [new() { Source = draft.Modifiers![0].Source, Value = draft.Modifiers[0].Value }]
                }));
            Assert.Equal(copies + 1, session.SnapshotCopies);
            Assert.False(session.HasChanges);
            Assert.Equal(expected, session.ReviewAssignment(draft));
            Assert.Equal(copies + 2, session.SnapshotCopies);

            session.Assign(draft);
            Assert.True(session.HasChanges);
            Assert.Equal(new(true, false, ""), session.ReviewAssignmentAvailability(draft));
            Assert.Equal(copies + 3, session.SnapshotCopies);
        }

        /// <summary>Changes to modifier values, edit settings and persisted content invalidate display reviews.</summary>
        [Fact]
        public void Assignment_availability_tracks_native_values_and_content_changes()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open("elite", issuer.Policy);
            var draft = AssignmentDraft(session);
            Assert.True(session.ReviewAssignmentAvailability(draft).Valid);
            var copies = session.SnapshotCopies;
            draft.Modifiers![0].Value = "Key_RightShift";
            Assert.True(session.ReviewAssignmentAvailability(draft).Valid);
            Assert.Equal(copies + 1, session.SnapshotCopies);
            var emptyInput = draft with { Key = "" };
            Assert.False(session.ReviewAssignmentAvailability(emptyInput).Valid);
            Assert.False(session.ReviewAssignmentAvailability(emptyInput).Valid);
            Assert.Equal(copies + 2, session.SnapshotCopies);

            Assert.True(session.ReviewAssignmentAvailability(draft).Valid);
            session.AcceptSaved();
            Assert.True(session.ReviewAssignmentAvailability(draft).Valid);
            session.Discard();
            Assert.True(session.ReviewAssignmentAvailability(draft).Valid);
            Assert.Equal(copies + 5, session.SnapshotCopies);
        }

        /// <summary>Display review checks edition access before cloning or validating the native profile.</summary>
        [Fact]
        public void Assignment_availability_checks_edition_before_reviewing()
        {
            var session = Open("elite", new EditionPolicy());
            var draft = AssignmentDraft(session);
            var copies = session.SnapshotCopies;
            var result = session.ReviewAssignmentAvailability(draft);
            Assert.False(result.Valid);
            Assert.Equal(session.AccessMessage, result.Message);
            Assert.Equal(copies, session.SnapshotCopies);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameAssignmentEdit AssignmentDraft(GameProfileSession session)
        {
            var devices = session.Snapshot.Profile.Devices;
            var stick = devices.Single(device => device.Identity!.SimSpecificIds["ed.device"] == "ExampleStick");
            var keyboard = devices.Single(device => device.Identity!.SimSpecificIds["ed.device"] == "Keyboard");
            return new(stick.LogicalId, "PrimaryFire", "Primary", "Joy_9",
                Modifiers: [new() { Source = keyboard.LogicalId, Value = "Key_LeftShift" }]);
        }

        private static GameProfileSession Open(string game, EditionPolicy policy)
        {
            ISimProfileBackend backend = game switch
            {
                "elite" => new EliteDangerousProfileBackend(), "squadrons" => new SquadronsProfileBackend(), _ => new WarThunderProfileBackend()
            };
            var relative = game switch
            {
                "elite" => Path.Combine("EliteDangerous", "Synthetic.4.2.binds"),
                "squadrons" => Path.Combine("Squadrons", SquadronsProfileBackend.FileName), _ => Path.Combine("WarThunder", "Synthetic.blk")
            };
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", relative);
            if (game == "squadrons") path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced");
            var profile = backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(relative)] = File.ReadAllText(path) });
            return new(backend, new(1, Guid.NewGuid(), "Baseline", profile, new()), policy, [], () => []);
        }
        #endregion END private helpers
    }
}
