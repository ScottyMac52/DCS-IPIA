using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>A deterministic owner queue that exposes notification recursion and wrong-thread calls.</summary>
    public sealed class WorkspaceTestScheduler : IWorkspaceScheduler
    {
        private readonly Queue<Action> _queue = new();
        #region BEGIN Public
        /// <summary>Controls a synthetic wrong-thread boundary.</summary>
        public bool HasAccess { get; set; } = true;
        /// <inheritdoc/>
        public bool CheckAccess() => HasAccess;
        /// <inheritdoc/>
        public void VerifyAccess() { if (!CheckAccess()) throw new InvalidOperationException("Wrong workspace thread."); }
        /// <inheritdoc/>
        public void Post(Action action) => _queue.Enqueue(action);
        /// <summary>Runs queued work without recursively invoking a currently publishing notification.</summary>
        public void Drain() { while (_queue.TryDequeue(out var action)) action(); }
        #endregion END Public
    }
}
