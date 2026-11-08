using Heimdall.App.Games.Profiles;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Application;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>DCS native hooks for the shared profile sequence: pinned/edition-checked candidate, one repository write, draft rebuild.</summary>
    internal sealed class DcsProfileOperations(DcsWorkspaceContext context, DcsWorkspaceSession session)
        : GameProfileOperationsBase<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot>(context, new DelegateInputLifetime(context.StopActivity))
    {
        /// <summary>DCS reports save outcomes through its native editor surfaces; the shared status is left unchanged.</summary>
        protected override string SavedMessage => "";
        /// <inheritdoc/>
        protected override string DiscardedMessage => "";
        /// <inheritdoc/>
        protected override DcsEditorWorkspaceSnapshot PrepareSave() => session.PrepareSave();
        /// <inheritdoc/>
        protected override async Task<WorkspaceCommit<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot>> PersistAsync(DcsEditorWorkspaceSnapshot candidate, IWorkspaceOperationLease lease)
        {
            await session.PersistAsync(candidate, lease);
            return new(candidate, candidate);
        }
        /// <inheritdoc/>
        protected override void AcceptCommit(WorkspaceCommit<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> commit, IWorkspaceOperationLease lease) => session.AcceptSaved(commit.Receipt, lease);
        /// <inheritdoc/>
        protected override void RestoreSaved(IWorkspaceOperationLease lease) => session.Discard(lease);
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed) { }
    }
}
