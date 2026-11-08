using Heimdall.Application.Games;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Content-keyed projections are reused across selection/no-op reads and recomputed for every real native change.</summary>
    public sealed class ProfileContentCacheTests
    {
        #region BEGIN Public
        /// <summary>Selection and repeated reads reuse one projection without whole-document serialization; edits, discard, key and session changes recompute.</summary>
        [Fact]
        public void Projection_is_reused_until_content_key_or_session_changes()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var context = new ProfileWorkspaceContextProbe();
            var session = WorkspaceHotPathTests.Open(issuer.Policy, 600); context.Session = session; context.Revision = "r";
            var cache = new ProfileContentCache<string, string>(() => context.Session, (document, key) => key + ":" + document.Name);
            Assert.Equal("a:Native", cache.Get("a", "")); Assert.Equal(1, cache.Projections);
            var commands = session.Catalog.Commands.Take(5).Select(command => command.Id).ToArray();
            context.NativeSelection.RefreshIds(null, commands);
            var before = GameWorkspaceContract.ThreadSerializations;
            foreach (var id in commands) { context.NativeSelection.SelectCommand(id); Assert.Equal("a:Native", cache.Get("a", "")); }
            Assert.Equal(1, cache.Projections); Assert.Equal(0, GameWorkspaceContract.ThreadSerializations - before);
            session.Rename("Edited");
            Assert.Equal("a:Edited", cache.Get("a", "")); Assert.Equal(2, cache.Projections);
            Assert.Equal("b:Edited", cache.Get("b", "")); Assert.Equal(3, cache.Projections);
            context.RunOperation(WorkspaceOperationKind.Discard, lease => { session.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); });
            Assert.Equal("b:Native", cache.Get("b", "")); Assert.Equal(4, cache.Projections);
            var other = WorkspaceHotPathTests.Open(issuer.Policy, 0); other.Rename("Other");
            context.Session = other;
            Assert.Equal("b:Other", cache.Get("b", "")); Assert.Equal(5, cache.Projections);
            context.Session = null;
            Assert.Equal("none", cache.Get("b", "none"));
        }
        #endregion END Public
    }
}
