using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
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
        #endregion END Public
        #region BEGIN private helpers
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
