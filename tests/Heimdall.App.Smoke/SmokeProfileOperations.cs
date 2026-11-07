using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Injected persistence hooks for non-game window probes; the shared base still owns the sequence.</summary>
    internal sealed class SmokeProfileOperations(IGameWorkspaceContext<string> context, Func<string> working, Func<Task> persist, Action restore)
        : GameProfileOperationsBase<string, bool>(context, new SmokeInput())
    {
        protected override string SavedMessage => "";
        protected override string DiscardedMessage => "";
        protected override string PrepareSave() => working();
        protected override async Task<WorkspaceCommit<string, bool>> PersistAsync(string candidate, IWorkspaceOperationLease lease) { await persist(); return new(candidate, true); }
        protected override void AcceptCommit(WorkspaceCommit<string, bool> commit, IWorkspaceOperationLease lease) { }
        protected override void RestoreSaved(IWorkspaceOperationLease lease) => restore();
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed) { }
        private sealed class SmokeInput : IWorkspaceInputLifetime { public void Stop() { } }
    }
}
