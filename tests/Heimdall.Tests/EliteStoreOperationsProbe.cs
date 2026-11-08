using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.EliteDangerous;

namespace Heimdall.Tests
{
    /// <summary>Store-backed ED hooks; optionally reopens the persisted preset model as a fresh native session like the ED adapter.</summary>
    public sealed class EliteStoreOperationsProbe(ProfileWorkspaceContextProbe context, IGameWorkspaceStore store, bool reopen, Heimdall.Application.EditionPolicy policy)
        : GameStoreProfileOperationsBase(context, () => store, new NoWorkspaceInput())
    {
        /// <inheritdoc/>
        protected override string UnsaveableMessage => "Open an ED preset.";
        /// <inheritdoc/>
        protected override string SavedMessage => "";
        /// <inheritdoc/>
        protected override string DiscardedMessage => "";
        /// <inheritdoc/>
        protected override void AcceptCommit(WorkspaceCommit<GameWorkspace, string> commit, IWorkspaceOperationLease lease)
        {
            if (!reopen) { base.AcceptCommit(commit, lease); return; }
            context.Session = new GameProfileSession(new EliteDangerousProfileBackend(), commit.Snapshot, policy, [], () => []); context.Revision = commit.Receipt;
        }
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed) { }
    }
}
