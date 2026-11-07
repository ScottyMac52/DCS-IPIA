using System.IO;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Runs the shared profile save/discard contract (P01–P05) against the production native hooks of all four adapters.</summary>
    internal static class ProfileOperationsConformanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            var root = Path.Combine(Path.GetTempPath(), "heimdall-profiles-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            IReadOnlyList<WorkspaceContextCase> cases = [];
            try
            {
                EditionAccess.SetForTests(issuer.PremiumPolicy); cases = WorkspaceContextCases.Create(root, issuer.PremiumPolicy);
                foreach (var fixture in cases)
                {
                    EditionAccess.SetForTests(issuer.PremiumPolicy); Verify(fixture);
                    Console.WriteLine("Profile operations conformance: " + fixture.Name + " passed.");
                }
            }
            finally
            {
                foreach (var fixture in cases) { fixture.Profiles.Dispose(); fixture.Context.Dispose(); }
                EditionAccess.SetForTests(previous); Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Verify(WorkspaceContextCase fixture)
        {
            var context = fixture.Context; var profiles = fixture.Profiles;
            var ancestor = profiles.GetType().BaseType;
            while (ancestor is not null && (!ancestor.IsGenericType || ancestor.GetGenericTypeDefinition() != typeof(GameProfileOperationsBase<,>))) ancestor = ancestor.BaseType;
            Require(ancestor is not null && ReferenceEquals(profiles.Context, context), fixture, "Native profile operations do not inherit the shared sequence over the same context.");
            Require(context.State.Open && !context.State.Dirty, fixture, "Native fixture must start at a saved baseline.");
            // P02: clean writable save is NoChange and never writes.
            var clean = Await(profiles.SaveAsync(default));
            Require(clean.Outcome == WorkspaceOperationOutcome.NoChange && !clean.Committed && fixture.Writes() == 0, fixture, "Clean save wrote or reported success.");
            var name = context.State.EditedName;
            // P03: persistence failure retains the draft, name and busy release; nothing is written.
            fixture.Edit("Pending profile edit"); context.SetEditedName(name + " pending");
            var pending = context.CaptureSnapshot();
            fixture.FailWrites(new IOException("Synthetic persistence failure"));
            Require(Throws<IOException>(() => Await(profiles.SaveAsync(default))), fixture, "Persistence failure was not surfaced.");
            Require(context.CaptureSnapshot() == pending && context.State.Dirty && context.State.Operation is null && fixture.Writes() == 0,
                fixture, "Failed save lost pending state, kept the operation or wrote.");
            fixture.FailWrites(null);
            if (context is GameProfileWorkspaceContextBase)
            {
                // P03: native name validation rejects before any write; the multi-sim adapters share GameConfigurationName.
                context.SetEditedName("   ");
                Require(Throws<Exception>(() => Await(profiles.SaveAsync(default))) && context.State.Dirty && fixture.Writes() == 0, fixture, "Invalid name was persisted.");
                context.SetEditedName(name + " pending");
            }
            // P01: retry persists exactly once, accepts the durable baseline and keeps the saved name.
            var saved = Await(profiles.SaveAsync(default));
            Require(saved.Outcome == WorkspaceOperationOutcome.Applied && saved.Committed && fixture.Writes() == 1, fixture, "Retry did not persist exactly once.");
            Require(!context.State.Dirty && context.State.EditedName == name + " pending" && context.State.Operation is null, fixture, "Accepted save did not establish the saved baseline and name.");
            Require(fixture.PersistedName() == name + " pending", fixture, "Durable readback did not match the accepted candidate.");
            var baseline = fixture.NativeSnapshot();
            Require(Await(profiles.SaveAsync(default)).Outcome == WorkspaceOperationOutcome.NoChange && fixture.Writes() == 1, fixture, "A second clean save wrote again.");
            // P05: discard restores the exact baseline without writing.
            fixture.Edit("Discard this edit");
            var discarded = Await(profiles.DiscardAsync(default));
            Require(discarded.Outcome == WorkspaceOperationOutcome.Applied && !context.State.Dirty && fixture.NativeSnapshot() == baseline && fixture.Writes() == 1,
                fixture, "Discard did not restore the saved baseline without writing.");
            Require(Await(profiles.DiscardAsync(default)).Outcome == WorkspaceOperationOutcome.NoChange, fixture, "Clean discard was not NoChange.");
            // P02: a restricted workspace is Blocked even when clean.
            fixture.MakeReadOnly();
            Require(Await(profiles.SaveAsync(default)).Outcome == WorkspaceOperationOutcome.Blocked && fixture.Writes() == 1, fixture, "Readonly save was not blocked.");
        }
        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try { action(); return false; }
            catch (TException) { return true; }
        }
        private static WorkspaceOperationResult Await(Task<WorkspaceOperationResult> operation)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!operation.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Profile operation did not complete.");
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            return operation.GetAwaiter().GetResult();
        }
        private static void Require(bool condition, WorkspaceContextCase fixture, string message)
        { if (!condition) throw new InvalidOperationException(fixture.Name + " profile operations: " + message); }
        #endregion END private helpers
    }
}
