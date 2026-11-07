using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>A durable in-memory store whose failures, conflicts and pauses are controlled by a case.</summary>
    public sealed class ProfileTestStore
    {
        private readonly Dictionary<string, (WorkspaceTestSnapshot Snapshot, int Revision)> _rows = new(StringComparer.Ordinal);
        #region BEGIN Public
        /// <summary>Counts durable writes.</summary>
        public int Writes { get; private set; }
        /// <summary>Throws before a durable write.</summary>
        public Exception? Fail { get; set; }
        /// <summary>Runs before a durable write, after preparation.</summary>
        public Func<Task>? BeforeWrite { get; set; }
        /// <summary>Runs after the durable write commits.</summary>
        public Action? AfterCommit { get; set; }
        /// <summary>Reads back the durable row.</summary>
        public WorkspaceTestSnapshot? Read(string key) => _rows.TryGetValue(key, out var row) ? row.Snapshot : null;
        /// <summary>Writes with optimistic revision checking.</summary>
        public async Task<int> WriteAsync(WorkspaceTestSnapshot candidate, int expected, CancellationToken cancellation)
        {
            if (BeforeWrite is { } before) await before();
            cancellation.ThrowIfCancellationRequested();
            if (Fail is { } fail) throw fail;
            var current = _rows.TryGetValue(candidate.Key, out var row) ? row.Revision : 0;
            if (current != expected) throw new InvalidOperationException("The profile was changed by another window.");
            _rows[candidate.Key] = (candidate, current + 1); Writes++;
            AfterCommit?.Invoke();
            return current + 1;
        }
        /// <summary>Simulates another writer advancing the revision.</summary>
        public void Advance(WorkspaceTestSnapshot snapshot) => _rows[snapshot.Key] = (snapshot, (_rows.TryGetValue(snapshot.Key, out var row) ? row.Revision : 0) + 1);
        #endregion END Public
    }

    /// <summary>Counts foreground input releases.</summary>
    public sealed class ProfileTestInput : IWorkspaceInputLifetime
    {
        #region BEGIN Public
        /// <summary>Gets the number of stops.</summary>
        public int Stops { get; private set; }
        /// <inheritdoc/>
        public void Stop() => Stops++;
        #endregion END Public
    }

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
        /// <summary>Counts accepted receipts and panel refreshes.</summary>
        public int Accepts { get; private set; }
        /// <summary>Counts panel refreshes.</summary>
        public int Refreshes { get; private set; }
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
        { Accepts++; Revision = commit.Receipt; context.Install(commit.Snapshot); }
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
            if (FailRefresh) throw new InvalidOperationException("Panel refresh failed.");
        }
    }
}
