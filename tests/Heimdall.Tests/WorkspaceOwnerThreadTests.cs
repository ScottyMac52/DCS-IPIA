using System.Collections.Concurrent;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>
    /// Pins the async owner-thread contract of the shared context base: commands start on the owner thread, whose
    /// SynchronizationContext (the WPF dispatcher in production) brings every continuation back for publication and lease teardown.
    /// </summary>
    public sealed class WorkspaceOwnerThreadTests
    {
        private sealed class OwnerLoop : SynchronizationContext, IWorkspaceScheduler, IDisposable
        {
            private readonly BlockingCollection<Action> _work = [];
            private readonly Thread _thread;
            internal OwnerLoop() { _thread = new(() => { foreach (var item in _work.GetConsumingEnumerable()) item(); }) { IsBackground = true }; _thread.Start(); }
            internal int ThreadId => _thread.ManagedThreadId;
            public override void Post(SendOrPostCallback d, object? state) => _work.Add(() => d(state));
            public bool CheckAccess() => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;
            public void VerifyAccess() { if (!CheckAccess()) throw new InvalidOperationException("The calling thread cannot access this object because a different thread owns it."); }
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
            public void Dispose() => _work.CompleteAdding();
        }

        public enum NativeEnd { Applied, Failed, Cancelled }

        [Theory, InlineData(NativeEnd.Applied), InlineData(NativeEnd.Failed), InlineData(NativeEnd.Cancelled)]
        public async Task Suspended_native_work_resumes_on_owner_for_publication_and_lease_release(NativeEnd end)
        {
            using var owner = new OwnerLoop();
            var notified = new ConcurrentQueue<int>();
            var (outcome, operationAfter, workerThread, admittedAgain) = await owner.Run(true, async () =>
            {
                var probe = new WorkspaceContextProbe(owner); probe.Open();
                probe.StateChanged += (_, _) => notified.Enqueue(Environment.CurrentManagedThreadId);
                using var cancel = new CancellationTokenSource();
                var worker = 0;
                Task<WorkspaceOperationResult> running = probe.RunOperationAsync(WorkspaceOperationKind.Export, async lease =>
                {
                    // Real suspension: native I/O off the owner thread, as DcsOutputCommand does with Task.Run.
                    worker = await Task.Run(() => { Thread.Sleep(20); return Environment.CurrentManagedThreadId; });
                    if (end == NativeEnd.Failed) throw new IOException("native write failed");
                    if (end == NativeEnd.Cancelled) { cancel.Cancel(); lease.Cancellation.ThrowIfCancellationRequested(); }
                    return new(WorkspaceOperationOutcome.Applied, "");
                }, cancel.Token);
                // Reentrant command while suspended is blocked and must not disturb the running lease.
                var blocked = await probe.RunOperationAsync(WorkspaceOperationKind.Save, _ => Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "")), default);
                Assert.Equal(WorkspaceOperationOutcome.Blocked, blocked.Outcome);
                string result;
                try { result = (await running).Outcome.ToString(); } catch (IOException) { result = "Failed"; }
                var again = await probe.RunOperationAsync(WorkspaceOperationKind.Save, _ => Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.NoChange, "")), default);
                return (result, probe.State.Operation, worker, again.Outcome);
            });
            Assert.Equal(end.ToString(), outcome);
            Assert.Null(operationAfter);
            Assert.NotEqual(owner.ThreadId, workerThread);
            Assert.Equal(WorkspaceOperationOutcome.NoChange, admittedAgain);
            Assert.NotEmpty(notified);
            Assert.All(notified, id => Assert.Equal(owner.ThreadId, id));
        }

        [Fact]
        public async Task An_owner_thread_without_its_synchronization_context_violates_the_contract_at_lease_release()
        {
            // The smoke harness defect: a bare STA thread with no dispatcher SynchronizationContext resumes on the pool.
            using var owner = new OwnerLoop();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.Run(false, async () =>
            {
                var probe = new WorkspaceContextProbe(owner); probe.Open();
                return await probe.RunOperationAsync(WorkspaceOperationKind.Export, async _ =>
                {
                    await Task.Run(() => Thread.Sleep(20));
                    return new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "");
                }, default);
            }));
            Assert.Contains("different thread owns it", error.Message, StringComparison.Ordinal);
        }
    }
}
