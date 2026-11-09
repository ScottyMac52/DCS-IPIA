using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Runs deterministic owner-thread callbacks with a bounded drain for selection-convergence tests.</summary>
    internal sealed class ConvergenceWorkspaceScheduler : IWorkspaceScheduler
    {
        private readonly Queue<Action> _queue = new();
        #region BEGIN Public
        /// <summary>Reports owner-thread access for this deterministic scheduler.</summary>
        public bool CheckAccess() => true;
        /// <summary>Accepts access on the deterministic test owner.</summary>
        public void VerifyAccess() { }
        /// <summary>Queues an owner-thread callback for deterministic execution.</summary>
        public void Post(Action action) => _queue.Enqueue(action);
        /// <summary>Executes at most the requested number of queued callbacks.</summary>
        public int Drain(int limit)
        {
            var count = 0;
            while (count < limit && _queue.TryDequeue(out var action)) { action(); count++; }
            return count;
        }
        #endregion END Public
    }
}
