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
}
