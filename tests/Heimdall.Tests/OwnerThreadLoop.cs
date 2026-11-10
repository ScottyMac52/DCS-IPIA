using System.Collections.Concurrent;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>A real owner thread with its own SynchronizationContext and owner-affine workspace scheduler.</summary>
    public sealed class OwnerThreadLoop : SynchronizationContext, IWorkspaceScheduler, IDisposable
    {
        #region BEGIN Public
        private readonly BlockingCollection<Action> _work = [];
        private readonly Thread _thread;
        /// <summary>Starts the owner thread.</summary>
        public OwnerThreadLoop() { _thread = new(() => { foreach (var item in _work.GetConsumingEnumerable()) item(); }) { IsBackground = true }; _thread.Start(); }
        internal int ThreadId => _thread.ManagedThreadId;
        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object? state) => _work.Add(() => d(state));
        /// <inheritdoc/>
        public bool CheckAccess() => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;
        /// <inheritdoc/>
        public void VerifyAccess() { if (!CheckAccess()) throw new InvalidOperationException("The calling thread cannot access this object because a different thread owns it."); }
        /// <inheritdoc/>
        public void Post(Action action) => _work.Add(action);
        /// <summary>Runs <paramref name="body"/> on the owner thread, optionally with its synchronization context installed.</summary>
        internal Task<T> Run<T>(bool installContext, Func<Task<T>> body)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Add(async () =>
            {
                SetSynchronizationContext(installContext ? this : null);
                try { done.SetResult(await body()); }
                catch (Exception error) { done.SetException(error); }
                finally { SetSynchronizationContext(null); }
            });
            return done.Task;
        }
        /// <inheritdoc/>
        public void Dispose() => _work.CompleteAdding();
        #endregion END Public
    }
}
