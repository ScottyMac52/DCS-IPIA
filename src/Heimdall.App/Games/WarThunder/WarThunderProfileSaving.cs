using Heimdall.App.Games.Profiles;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>War Thunder hooks for the shared profile sequence; source-preserving review output stays a separate command.</summary>
    internal sealed class WarThunderProfileSaving(WarThunderWorkspaceContext context, Action stopInput, Action afterSave, Action afterDiscard)
        : GameStoreProfileOperationsBase(context, () => context.Services.Store, new DelegateInputLifetime(stopInput))
    {
        /// <inheritdoc/>
        protected override string UnsaveableMessage => "This War Thunder workspace cannot currently be saved.";
        /// <inheritdoc/>
        protected override string SavedMessage => "War Thunder profile saved.";
        /// <inheritdoc/>
        protected override string DiscardedMessage => "";
        /// <inheritdoc/>
        protected override void ValidateCandidate(GameWorkspace candidate)
        {
            if (context.ScanningHardware) throw new InvalidOperationException(UnsaveableMessage);
            base.ValidateCandidate(candidate);
        }
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed)
        {
            if (completed == WorkspaceOperationKind.Discard) afterDiscard();
            else afterSave();
        }
    }
}
