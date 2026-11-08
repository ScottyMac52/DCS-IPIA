using System.IO;
using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.App.Games.Dcs;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises production DCS persistence after genuine suspension without a caller synchronization context.</summary>
    internal static class DcsPersistenceCompletionSmoke
    {
        #region BEGIN Public
        internal static void Verify(string root)
        {
            var connection = "Data Source=" + Path.Combine(root, "dcs-owner.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var workspace = MixedSnapshotFixture.Create(); repository.SaveAsync(workspace).GetAwaiter().GetResult();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var counter = new CountingDcsRepository(repository) { BeforeWrite = () => release.Task };
            var session = new DcsWorkspaceSession(new(workspace), counter, new EmptyDeviceDiscovery(), null, null, null, null, null, null, null, null, null);
            using var context = new DcsWorkspaceContext(session, new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher), () => Task.CompletedTask, () => { });
            using var profiles = new DcsProfileOperations(context, session);
            var owner = Environment.CurrentManagedThreadId; var notifications = new List<int>();
            session.StateChanged += (_, _) => notifications.Add(Environment.CurrentManagedThreadId);
            session.StatusChanged += (_, _) => notifications.Add(Environment.CurrentManagedThreadId);
            session.SetLabel("async-owner", "durable");
            var previous = SynchronizationContext.Current; Task<WorkspaceOperationResult> running;
            SynchronizationContext.SetSynchronizationContext(null);
            try { running = profiles.SaveAsync(default); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            if (running.IsCompleted) throw new InvalidOperationException("DCS persistence fixture did not suspend.");
            release.SetResult(); var saved = Await(running);
            if (!saved.Committed || saved.RecoveryRequired || counter.Writes != 1 || context.State.Dirty || context.State.Operation is not null
                || notifications.Any(thread => thread != owner)) throw new InvalidOperationException("DCS durable completion did not stay on its owner.");
            var durable = repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult();
            if (durable?.Labels["async-owner"] != "durable") throw new InvalidOperationException("DCS completion lost the durable label.");
            // A failure after the same suspended boundary releases editing and permits a single-write retry.
            session.SetLabel("async-owner", "retry");
            release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            counter.BeforeWrite = () => release.Task; counter.Fail = new IOException("async write failure");
            SynchronizationContext.SetSynchronizationContext(null);
            try { running = profiles.SaveAsync(default); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            if (running.IsCompleted) throw new InvalidOperationException("DCS failure fixture did not suspend.");
            release.SetResult();
            try { Await(running); throw new InvalidOperationException("DCS write failure was hidden."); }
            catch (IOException) { }
            counter.Fail = null;
            if (context.State.Operation is not null || !context.State.Dirty || counter.Writes != 1 || notifications.Any(thread => thread != owner))
                throw new InvalidOperationException("DCS failed completion lost its draft or ran off-owner.");
            if (!Await(profiles.SaveAsync(default)).Committed || counter.Writes != 2) throw new InvalidOperationException("DCS could not retry exactly once.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceOperationResult Await(Task<WorkspaceOperationResult> task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("DCS owner completion did not finish.");
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            return task.GetAwaiter().GetResult();
        }
        #endregion END private helpers
    }
}
