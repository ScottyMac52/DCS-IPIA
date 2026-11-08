using System.Collections.Concurrent;
using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>
    /// Pins the async owner-thread contract of the shared context base: commands start on the owner thread, whose
    /// SynchronizationContext (the WPF dispatcher in production) brings every continuation back for publication and lease teardown.
    /// </summary>
    public sealed class WorkspaceOwnerThreadTests
    {
        #region BEGIN Public

        /// <summary>Applied, failed and cancelled native work resumes on the owner for publication and lease release.</summary>
        [Theory, InlineData(OwnerThreadNativeEnd.Applied), InlineData(OwnerThreadNativeEnd.Failed), InlineData(OwnerThreadNativeEnd.Cancelled)]
        public async Task Suspended_native_work_resumes_on_owner_for_publication_and_lease_release(OwnerThreadNativeEnd end)
        {
            using var owner = new OwnerThreadLoop();
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
                    if (end == OwnerThreadNativeEnd.Failed) throw new IOException("native write failed");
                    if (end == OwnerThreadNativeEnd.Cancelled) { cancel.Cancel(); lease.Cancellation.ThrowIfCancellationRequested(); }
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

        /// <summary>A completion off the owner thread is finished centrally on the owner.</summary>
        [Fact]
        public async Task Completion_off_the_owner_thread_is_finished_centrally_on_the_owner_without_stranding_the_operation()
        {
            // A caller that started without the owner SynchronizationContext resumes on the pool. The shared base must still
            // publish, complete and release on the owner thread, keep the access check, and surface the durable result.
            using var owner = new OwnerThreadLoop();
            var notified = new ConcurrentQueue<int>();
            WorkspaceContextProbe? probe = null;
            var result = await owner.Run(false, async () =>
            {
                probe = new WorkspaceContextProbe(owner); probe.Open();
                probe.StateChanged += (_, _) => notified.Enqueue(Environment.CurrentManagedThreadId);
                return await probe.RunOperationAsync(WorkspaceOperationKind.Save, async _ =>
                {
                    await Task.Run(() => Thread.Sleep(20));
                    return new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "durable", true);
                }, default);
            });
            Assert.True(result.Committed);
            var state = await owner.Run(true, () => Task.FromResult(probe!.State));
            Assert.Null(state.Operation);
            Assert.All(notified, id => Assert.Equal(owner.ThreadId, id));
            Assert.Throws<InvalidOperationException>(() => probe!.Dispose());
            await owner.Run(true, () => { probe!.Dispose(); return Task.FromResult(0); });
        }
        #endregion END Public
    }
}
