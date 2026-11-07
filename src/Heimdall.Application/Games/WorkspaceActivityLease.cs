using System.Diagnostics;
using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Releases native input once and prevents stale cleanup from clearing a newer activity.</summary>
    internal sealed class WorkspaceActivityLease : IWorkspaceActivityLease
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly List<Action> _cleanup = [];
        private bool _disposed;
        private readonly Action<WorkspaceActivityLease> _released;
        internal WorkspaceActivityLease(WorkspaceIdentity identity, WorkspaceActivityKind kind, Action<WorkspaceActivityLease> released)
        { Identity = identity; Kind = kind; _released = released; Cancellation = _cancellation.Token; }
        #region BEGIN Public
        /// <inheritdoc/>
        public WorkspaceIdentity Identity { get; }
        /// <inheritdoc/>
        public WorkspaceActivityKind Kind { get; }
        /// <inheritdoc/>
        public CancellationToken Cancellation { get; }
        /// <inheritdoc/>
        public void RegisterCleanup(Action cleanup)
        {
            ArgumentNullException.ThrowIfNull(cleanup);
            if (_disposed) Release(cleanup); else _cleanup.Add(cleanup);
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cancellation.Cancel(); }
            catch (AggregateException error) { Trace.TraceError("Activity cancellation failed: {0}", error); }
            finally
            {
                for (var index = _cleanup.Count - 1; index >= 0; index--) Release(_cleanup[index]);
                _cleanup.Clear(); _cancellation.Dispose(); _released(this);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Release(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception error) { Trace.TraceError("Activity cleanup failed: {0}", error); }
        }
        #endregion END private helpers
    }
}
