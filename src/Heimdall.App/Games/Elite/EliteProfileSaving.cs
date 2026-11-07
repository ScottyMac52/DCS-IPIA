using Heimdall.App.Games.Profiles;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Elite Dangerous hooks for the shared profile sequence; reviewed preset replacement stays a separate export command.</summary>
    internal sealed class EliteProfileSaving(EliteWorkspaceContext context, Action stopInput, Func<string> profileName, Action<GameWorkspace, string> reopen,
        Func<string> savedMessage, Action afterSave, Action afterDiscard)
        : GameStoreProfileOperationsBase(context, () => context.Services.Store, new DelegateInputLifetime(stopInput))
    {
        /// <inheritdoc/>
        protected override string CandidateName => profileName();
        /// <inheritdoc/>
        protected override string UnsaveableMessage => "Open an Elite configuration before saving.";
        /// <inheritdoc/>
        protected override string SavedMessage => savedMessage();
        /// <inheritdoc/>
        protected override string DiscardedMessage => "";
        /// <inheritdoc/>
        protected override void ValidateCandidate(GameWorkspace candidate) { GameWorkspaceContract.Validate(candidate); base.ValidateCandidate(candidate); }
        /// <inheritdoc/>
        protected override void AcceptCommit(WorkspaceCommit<GameWorkspace, string> commit, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(commit);
            reopen(commit.Snapshot, commit.Receipt); // ED installs the persisted preset model as a fresh native session.
        }
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed)
        {
            if (completed == WorkspaceOperationKind.Discard) afterDiscard();
            else afterSave();
        }
    }
}
