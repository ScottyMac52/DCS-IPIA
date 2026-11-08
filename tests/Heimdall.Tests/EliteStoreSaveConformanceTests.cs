using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Real ED session over the durable file store: clean save is NoChange with zero writes even when other
    /// workspaces share the store; an edit persists once and reads back; ED reopen-after-save keeps a clean baseline.</summary>
    public sealed class EliteStoreSaveConformanceTests
    {
        #region BEGIN Public
        /// <summary>Isolated per-store write counts expose only this workspace's persistence.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Clean_save_writes_nothing_and_edit_round_trips(bool reopen)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var root = Path.Combine(Path.GetTempPath(), "heimdall-ed-store-" + Guid.NewGuid().ToString("N"));
            try
            {
                var files = new FileGameWorkspaceStore(root);
                var mine = new CountingStore(files); var other = new CountingStore(files);
                using var context = new ProfileWorkspaceContextProbe();
                var session = WorkspaceHotPathTests.Open(issuer.Policy, 0);
                context.Session = session; context.Revision = mine.Save(session.Snapshot, null);
                var start = mine.Writes;
                // Another game's fixture saving into the same durable store must not count as this workspace's write.
                _ = other.Save(WorkspaceHotPathTests.Open(issuer.Policy, 0).Snapshot with { Name = "Other game" }, null);
                using var profiles = new StoreOperations(context, mine, reopen, issuer.Policy);
                Assert.False(context.State.Dirty);
                var clean = await profiles.SaveAsync(CancellationToken.None);
                Assert.Equal(WorkspaceOperationOutcome.NoChange, clean.Outcome); Assert.False(clean.Committed); Assert.Equal(0, mine.Writes - start);
                context.SetEditedName("Renamed ED");
                var saved = await profiles.SaveAsync(CancellationToken.None);
                Assert.True(saved.Committed); Assert.Equal(1, mine.Writes - start); Assert.False(context.State.Dirty);
                Assert.Equal("Renamed ED", files.Load(session.Snapshot.Id).Workspace.Name);
                Assert.Equal(WorkspaceOperationOutcome.NoChange, (await profiles.SaveAsync(CancellationToken.None)).Outcome); Assert.Equal(1, mine.Writes - start);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public

        #region BEGIN private helpers
        private sealed class CountingStore(IGameWorkspaceStore inner) : IGameWorkspaceStore
        {
            public int Writes { get; private set; }
            public IReadOnlyList<GameWorkspaceEntry> List() => inner.List();
            public GameWorkspaceRead Load(Guid id) => inner.Load(id);
            public string Save(GameWorkspace workspace, string? expectedRevision) { var revision = inner.Save(workspace, expectedRevision); Writes++; return revision; }
            public void Delete(Guid id, string expectedRevision) => inner.Delete(id, expectedRevision);
            public GameSelectionState ReadSelection() => inner.ReadSelection();
            public void SaveSelection(GameSelectionState selection) => inner.SaveSelection(selection);
        }
        private sealed class StoreOperations(ProfileWorkspaceContextProbe context, IGameWorkspaceStore store, bool reopen, Heimdall.Application.EditionPolicy policy)
            : GameStoreProfileOperationsBase(context, () => store, new NoInput())
        {
            protected override string UnsaveableMessage => "Open an ED preset.";
            protected override string SavedMessage => "";
            protected override string DiscardedMessage => "";
            protected override void AcceptCommit(WorkspaceCommit<GameWorkspace, string> commit, IWorkspaceOperationLease lease)
            {
                if (!reopen) { base.AcceptCommit(commit, lease); return; }
                // ED reopens the persisted preset model as a fresh native session.
                context.Session = new GameProfileSession(new EliteDangerousProfileBackend(), commit.Snapshot, policy, [], () => []); context.Revision = commit.Receipt;
            }
            protected override void RefreshAfterPersistence(WorkspaceOperationKind completed) { }
        }
        private sealed class NoInput : IWorkspaceInputLifetime { public void Stop() { } }
        #endregion END private helpers
    }
}
