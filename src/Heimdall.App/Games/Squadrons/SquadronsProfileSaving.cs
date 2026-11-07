using Heimdall.App.Games.Profiles;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Squadrons hooks for the shared profile sequence: store revision, remembered selection and separate review output untouched.</summary>
    internal sealed class SquadronsProfileSaving(SquadronsWorkspaceContext context, Action stopInput, Action afterSave, Action afterDiscard)
        : GameStoreProfileOperationsBase(context, () => context.Services.Store, new DelegateInputLifetime(stopInput))
    {
        /// <inheritdoc/>
        protected override string UnsaveableMessage => "This Squadrons workspace cannot currently be saved.";
        /// <inheritdoc/>
        protected override string SavedMessage => "Squadrons profile saved.";
        /// <inheritdoc/>
        protected override string DiscardedMessage => "Draft changes discarded.";
        /// <inheritdoc/>
        protected override void RestoreSaved(IWorkspaceOperationLease lease)
        {
            base.RestoreSaved(lease);
            if (context.Revision is null) context.Session = null; // An unsaved import has no baseline to return to.
        }
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed)
        {
            if (completed == WorkspaceOperationKind.Discard) afterDiscard();
            else afterSave();
        }
    }
}
