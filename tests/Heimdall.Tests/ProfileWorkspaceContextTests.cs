using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects borrowed native save/discard leases, baseline readback and retired-session isolation.</summary>
    public sealed class ProfileWorkspaceContextTests
    {
        #region BEGIN Public
        /// <summary>Native references cannot edit through a pending save; successful acceptance requires the real issued lease.</summary>
        [Fact]
        public async Task Native_session_mutations_observe_the_context_operation_and_persisted_baseline()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var context = new ProfileWorkspaceContextProbe();
            var native = Open(issuer.Policy); context.Session = native; context.Revision = "original";
            Assert.False(context.Chrome.Pending); Assert.True(context.Chrome.CanExport);
            context.SetEditedName("Edited"); var saved = context.Saved;
            await Assert.ThrowsAsync<IOException>(() => context.RunOperationAsync(WorkspaceOperationKind.Save, lease =>
            {
                Assert.True(context.OwnsOperation(lease));
                Assert.Throws<InvalidOperationException>(() => native.Rename("bypass"));
                Assert.Throws<InvalidOperationException>(native.Discard);
                Assert.Throws<InvalidOperationException>(native.AcceptSaved);
                Assert.Throws<InvalidOperationException>(() => native.Discard(lease));
                throw new IOException("Synthetic write failure");
            }, default));
            Assert.Same(saved, context.Saved); Assert.Equal("original", context.Revision); Assert.Equal("Edited", context.State.EditedName);
            var result = await context.RunOperationAsync(WorkspaceOperationKind.Save, lease =>
            {
                native.AcceptSaved(native.Snapshot with { Name = context.State.EditedName }, lease); context.Revision = "accepted";
                return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "", true));
            }, default);
            Assert.True(result.Committed); Assert.False(context.State.Dirty); Assert.Equal("Edited", context.Saved!.Name);
            native.Rename("Native dirty"); Assert.True(context.State.Dirty);
            context.RunOperation(WorkspaceOperationKind.Discard, lease => { native.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); });
            Assert.False(context.State.Dirty); Assert.Equal("Edited", native.Snapshot.Name);
        }
        /// <summary>Switching detaches old subscriptions; readonly, unpersisted and disposed sessions retain explicit state.</summary>
        [Fact]
        public void Replacement_and_disposal_cannot_leave_a_retired_native_session_mounted()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var context = new ProfileWorkspaceContextProbe();
            Assert.False(context.Chrome.HasProfile);
            var old = Open(issuer.Policy); context.Session = old; context.Revision = null; Assert.True(context.State.Dirty);
            context.Revision = "saved"; Assert.False(context.State.Dirty);
            context.Session = Open(issuer.Policy); Assert.Throws<InvalidOperationException>(() => old.Rename("stale"));
            var epoch = context.State.Identity;
            context.Session = context.Session; Assert.Equal(epoch, context.State.Identity);
            context.Session = Open(new()); context.Revision = "readonly"; Assert.True(context.State.ReadOnly);
            Assert.False(context.State.Capabilities.Save.Enabled);
            context.Session = null; Assert.False(context.State.Open); Assert.Equal("No profile", context.Chrome.Status);
            context.Session = Open(issuer.Policy); var mounted = context.Session; context.Dispose();
            Assert.Throws<InvalidOperationException>(() => mounted.Rename("after disposal"));
        }
        /// <summary>A durable receipt is accepted even when the owner disposes while the write is completing.</summary>
        [Fact]
        public async Task Disposal_during_a_durable_write_does_not_lose_the_native_receipt()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var context = new ProfileWorkspaceContextProbe();
            context.Session = Open(issuer.Policy); var native = context.Session;
            var result = await context.RunOperationAsync(WorkspaceOperationKind.Save, lease =>
            {
                var candidate = native.Snapshot with { Name = "Durable" };
                context.Dispose(); native.AcceptSaved(candidate, lease);
                return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "", true));
            }, default);
            Assert.True(result.Committed); Assert.Equal("Durable", native.Snapshot.Name); Assert.Null(context.State.Operation);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameProfileSession Open(Heimdall.Application.EditionPolicy policy)
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { ["controls.binds"] = "<Root PresetName=\"Test\" MajorVersion=\"4\" MinorVersion=\"0\" />" });
            return new(backend, new(1, Guid.NewGuid(), "Native", profile, new()), policy, [], () => []);
        }
        #endregion END private helpers
    }
}
