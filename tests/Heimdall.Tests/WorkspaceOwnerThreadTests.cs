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
        [Theory]
        [InlineData(OwnerThreadNativeEnd.Applied, true)]
        [InlineData(OwnerThreadNativeEnd.Failed, true)]
        [InlineData(OwnerThreadNativeEnd.Cancelled, true)]
        [InlineData(OwnerThreadNativeEnd.Applied, false)]
        [InlineData(OwnerThreadNativeEnd.Failed, false)]
        [InlineData(OwnerThreadNativeEnd.Cancelled, false)]
        public async Task Suspended_native_work_resumes_on_owner_for_publication_and_lease_release(OwnerThreadNativeEnd end, bool ownerContext)
        {
            // Callers start on the owner; without the owner context only the shared base's guarantees are under test, so the
            // test body itself never touches the context off the owner thread.
            using var owner = new OwnerThreadLoop();
            using var cancel = new CancellationTokenSource();
            var notified = new ConcurrentQueue<int>();
            var worker = 0;
            var (probe, running) = await owner.Run(ownerContext, () =>
            {
                var probe = new WorkspaceContextProbe(owner); probe.Open();
                probe.StateChanged += (_, _) => notified.Enqueue(Environment.CurrentManagedThreadId);
                var running = probe.RunOperationAsync(WorkspaceOperationKind.Export, async lease =>
                {
                    // Real suspension: native I/O off the owner thread, as DcsOutputCommand does with Task.Run.
                    worker = await Task.Run(() => { Thread.Sleep(20); return Environment.CurrentManagedThreadId; });
                    if (end == OwnerThreadNativeEnd.Failed) throw new IOException("native write failed");
                    if (end == OwnerThreadNativeEnd.Cancelled) { await cancel.CancelAsync(); lease.Cancellation.ThrowIfCancellationRequested(); }
                    return new(WorkspaceOperationOutcome.Applied, "");
                }, cancel.Token);
                // A reentrant command while the first is suspended is blocked and must not disturb the running lease.
                var blocked = probe.RunOperationAsync(WorkspaceOperationKind.Save, _ => Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "")), default);
                Assert.True(blocked.IsCompleted); Assert.Equal(WorkspaceOperationOutcome.Blocked, blocked.Result.Outcome);
                return Task.FromResult((probe, running));
            });
            string outcome;
            try { outcome = (await running.WaitAsync(TimeSpan.FromSeconds(10))).Outcome.ToString(); } catch (IOException) { outcome = "Failed"; }
            var (operationAfter, admittedAgain) = await owner.Run(true, async () =>
            {
                var again = await probe.RunOperationAsync(WorkspaceOperationKind.Save, _ => Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.NoChange, "")), default);
                var state = (probe.State.Operation, again.Outcome);
                probe.Dispose(); return state;
            });
            Assert.Equal(end.ToString(), outcome);
            Assert.Null(operationAfter);
            Assert.NotEqual(owner.ThreadId, worker);
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
        /// <summary>Native accept and command acceptance run on the owner even when the caller resumed elsewhere.</summary>
        [Theory, InlineData(true), InlineData(false)]
        public async Task Native_accept_after_real_suspension_runs_on_the_owner(bool ownerContext)
        {
            using var owner = new OwnerThreadLoop();
            var store = new ProfileTestStore { BeforeWrite = () => Task.Run(() => Thread.Sleep(20)) };
            var (context, profiles, saving) = await owner.Run(ownerContext, () =>
            {
                var context = new WorkspaceContextProbe(owner); context.Open();
                var profiles = new ProfileOperationsProbe(context, store, new ProfileTestInput());
                context.SetEditedName("Durable");
                return Task.FromResult((context, profiles, profiles.SaveAsync(default)));
            });
            var saved = await saving.WaitAsync(TimeSpan.FromSeconds(10));
            var (command, running) = await owner.Run(ownerContext, () =>
            {
                var command = new ProfileCommandProbe(context, new ProfileTestInput()) { DuringRun = () => Task.Run(() => Thread.Sleep(20)) };
                return Task.FromResult((command, command.ExecuteAsync("out", default)));
            });
            Assert.True((await running.WaitAsync(TimeSpan.FromSeconds(10))).Native);
            var (operation, name) = await owner.Run(true, () =>
            {
                var state = (context.State.Operation, context.Working!.Name);
                profiles.Dispose(); context.Dispose(); return Task.FromResult(state);
            });
            Assert.True(saved.Committed); Assert.Null(operation); Assert.Equal("Durable", name);
            Assert.Equal(1, store.Writes);
            Assert.Equal(owner.ThreadId, profiles.AcceptThreadId);
            Assert.Equal(owner.ThreadId, command.AcceptThreadId);
        }

        /// <summary>A pre- or partial-install failure after a real suspended write enters recovery, seals and publishes on the owner,
        /// releases the lease, keeps the durable write single, and the next reopen clears recovery on the owner.</summary>
        [Theory, InlineData(false, true), InlineData(true, true), InlineData(false, false), InlineData(true, false)]
        public async Task Recovery_transition_after_real_suspension_runs_on_the_owner(bool partial, bool ownerContext)
        {
            using var owner = new OwnerThreadLoop();
            var store = new ProfileTestStore { BeforeWrite = () => Task.Run(() => Thread.Sleep(20)) };
            var stateThreads = new System.Collections.Concurrent.ConcurrentBag<int>();
            var (context, profiles, saving) = await owner.Run(ownerContext, () =>
            {
                var context = new WorkspaceContextProbe(owner); context.Open();
                var profiles = new ProfileOperationsProbe(context, store, new ProfileTestInput()) { FailInstall = partial };
                context.StateChanged += (_, _) => stateThreads.Add(Environment.CurrentManagedThreadId);
                context.StatusChanged += (_, _) => stateThreads.Add(Environment.CurrentManagedThreadId);
                context.SetEditedName("Durable");
                return Task.FromResult((context, profiles, profiles.SaveAsync(default)));
            });
            var saved = await saving.WaitAsync(TimeSpan.FromSeconds(10));
            var (recovery, operation, renameRejected) = await owner.Run(true, () =>
            {
                var rejected = Record.Exception(() => context.SetEditedName("stale")) is InvalidOperationException;
                return Task.FromResult((context.State.Recovery, context.State.Operation, rejected));
            });
            Assert.True(saved.Committed); Assert.True(saved.RecoveryRequired);
            Assert.NotEmpty(recovery); Assert.Null(operation); Assert.True(renameRejected);
            Assert.Equal(1, store.Writes); Assert.Equal("Durable", store.Read("native:key")!.Name);
            Assert.Equal(owner.ThreadId, context.SealThreadId);
            Assert.All(stateThreads, thread => Assert.Equal(owner.ThreadId, thread));
            var cleared = await owner.Run(true, () =>
            {
                context.Reopen(store.Read("native:key")!); var state = context.State.Recovery;
                profiles.Dispose(); context.Dispose(); return Task.FromResult(state);
            });
            Assert.Empty(cleared); Assert.Equal(1, store.Writes); Assert.Equal(1, context.Disposals);
        }

        /// <summary>Durable command acceptance failures after real suspension publish committed recovery/warnings on the owner.</summary>
        [Theory]
        [InlineData(ProfileCommandEffects.WorkspaceWrite, true)]
        [InlineData(ProfileCommandEffects.WorkspaceWrite, false)]
        [InlineData(ProfileCommandEffects.OutputWrite, true)]
        [InlineData(ProfileCommandEffects.OutputWrite, false)]
        public async Task Command_postcommit_failure_resumes_on_owner(ProfileCommandEffects effects, bool ownerContext)
        {
            using var owner = new OwnerThreadLoop();
            var writes = 0; var threads = new ConcurrentQueue<int>();
            var (context, command, running) = await owner.Run(ownerContext, () =>
            {
                var context = new WorkspaceContextProbe(owner); context.Open();
                context.StateChanged += (_, _) => threads.Enqueue(Environment.CurrentManagedThreadId);
                context.StatusChanged += (_, _) => threads.Enqueue(Environment.CurrentManagedThreadId);
                var command = new ProfileCommandProbe(context, new())
                {
                    Effects = effects, DuringRun = () => Task.Run(() => { Thread.Sleep(20); writes++; }),
                    DuringAccept = () => throw new IOException("completion failed")
                };
                return Task.FromResult((context, command, command.ExecuteAsync("receipt", default)));
            });
            var result = await running.WaitAsync(TimeSpan.FromSeconds(10));
            var recovery = await owner.Run(true, () =>
            {
                Assert.Null(context.State.Operation); var recovery = context.State.Recovery.Length > 0;
                context.Dispose(); return Task.FromResult(recovery);
            });
            Assert.True(result.Native); Assert.True(result.Completion.Committed);
            Assert.Equal(effects == ProfileCommandEffects.WorkspaceWrite, result.Completion.RecoveryRequired);
            Assert.Equal(result.Completion.RecoveryRequired, recovery); Assert.Equal(1, writes);
            Assert.Equal(owner.ThreadId, command.AcceptThreadId);
            Assert.All(threads, thread => Assert.Equal(owner.ThreadId, thread));
        }

        /// <summary>Disposal that runs on the owner before a queued off-thread completion releases native state exactly once without stranding the lease.</summary>
        [Fact]
        public async Task Disposal_while_completion_is_queued_releases_once_and_reports_the_result()
        {
            using var owner = new OwnerThreadLoop();
            var native = new TaskCompletionSource<WorkspaceOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            WorkspaceContextProbe? probe = null; Task<WorkspaceOperationResult>? running = null;
            await owner.Run(false, () =>
            {
                probe = new WorkspaceContextProbe(owner); probe.Open();
                running = probe.RunOperationAsync(WorkspaceOperationKind.Save, async _ => await native.Task.ConfigureAwait(false), default);
                return Task.FromResult(0);
            });
            using var gate = new ManualResetEventSlim();
            owner.Post(() => gate.Wait());                       // 1: hold the owner
            owner.Post(() => probe!.Dispose());                  // 2: disposal queued first
            native.SetResult(new(WorkspaceOperationOutcome.Applied, "durable", true)); // 3: completion arrives off-owner and is queued
            await Task.Delay(50); gate.Set();
            var result = await running!.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result.Committed);
            var (operation, open, disposals) = await owner.Run(true, () => Task.FromResult((probe!.State.Operation, probe.State.Open, probe.Disposals)));
            Assert.Null(operation); Assert.False(open); Assert.Equal(1, disposals);
        }
        #endregion END Public
    }
}
