using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>A native adapter supplying only typed hooks to the shared profile sequence.</summary>
    public sealed class ProfileOperationsProbe(WorkspaceContextProbe context, ProfileTestStore store, ProfileTestInput input)
        : GameProfileOperationsBase<WorkspaceTestSnapshot, int>(context, input)
    {
        #region BEGIN Public
        /// <summary>Gets the accepted native revision.</summary>
        public int Revision { get; private set; }
        /// <summary>Rejects the next candidate with a native validation message.</summary>
        public string? RejectCandidate { get; set; }
        /// <summary>Fails native restoration before swapping state.</summary>
        public bool FailRestore { get; set; }
        /// <summary>Throws from the post-commit panel refresh.</summary>
        public bool FailRefresh { get; set; }
        /// <summary>Installs accepted commits as a fresh native session instead of accepting into the open one.</summary>
        public bool ReopenOnAccept { get; set; }
        /// <summary>Counts accepted receipts and panel refreshes.</summary>
        public int Accepts { get; private set; }
        /// <summary>Counts panel refreshes.</summary>
        public int Refreshes { get; private set; }
        /// <summary>Gets the managed thread that installed the last accepted commit.</summary>
        public int AcceptThreadId { get; private set; }
        /// <summary>Gets or sets an accept failure: false fails before the native install, true after it (partial install).</summary>
        public bool? FailInstall { get; set; }
        /// <summary>Gets the last prepared candidate.</summary>
        public WorkspaceTestSnapshot? Candidate { get; private set; }
        #endregion END Public
        /// <inheritdoc/>
        protected override string SavedMessage => "Probe profile saved.";
        /// <inheritdoc/>
        protected override string DiscardedMessage => "Probe edits discarded.";
        /// <inheritdoc/>
        protected override WorkspaceTestSnapshot PrepareSave()
        {
            if (RejectCandidate is { } reason) throw new InvalidOperationException(reason);
            return Candidate = context.Working! with { Name = context.State.EditedName };
        }
        /// <inheritdoc/>
        protected override async Task<WorkspaceCommit<WorkspaceTestSnapshot, int>> PersistAsync(WorkspaceTestSnapshot candidate, IWorkspaceOperationLease lease)
            => new(candidate, await store.WriteAsync(candidate, Revision, lease.Cancellation));
        /// <inheritdoc/>
        protected override void AcceptCommit(WorkspaceCommit<WorkspaceTestSnapshot, int> commit, IWorkspaceOperationLease lease)
        {
            Accepts++; Revision = commit.Receipt; AcceptThreadId = Environment.CurrentManagedThreadId;
            if (FailInstall == false) throw new InvalidOperationException("native install failed");
            if (ReopenOnAccept) context.Reopen(commit.Snapshot); else context.Install(commit.Snapshot);
            if (FailInstall == true) throw new InvalidOperationException("native install failed part-way");
        }
        /// <inheritdoc/>
        protected override void RestoreSaved(IWorkspaceOperationLease lease)
        {
            if (FailRestore) throw new InvalidOperationException("Native baseline could not be prepared.");
            context.Restore();
        }
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed)
        {
            Refreshes++;
            context.RefreshNativeState(); // Native panels re-read state during refresh, as the window adapters do.
            if (FailRefresh) throw new InvalidOperationException("Panel refresh failed.");
        }
    }
}
