using System.IO;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Runs the same state, scope, gates, baseline, subscription and disposal assertions against all four native adapters.</summary>
    internal static class WorkspaceContextConformanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            var root = Path.Combine(Path.GetTempPath(), "heimdall-contexts-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            IReadOnlyList<WorkspaceContextCase> cases = [];
            IReadOnlyList<WorkspaceContextCase> presentationCases = [];
            try
            {
                EditionAccess.SetForTests(issuer.PremiumPolicy); cases = WorkspaceContextCases.Create(root, issuer.PremiumPolicy);
                foreach (var fixture in cases) { EditionAccess.SetForTests(issuer.PremiumPolicy); Verify(fixture); }
                var presentationRoot = Path.Combine(root, "presentation"); Directory.CreateDirectory(presentationRoot);
                EditionAccess.SetForTests(issuer.PremiumPolicy); presentationCases = WorkspaceContextCases.Create(presentationRoot, issuer.PremiumPolicy);
                foreach (var fixture in presentationCases)
                {
                    EditionAccess.SetForTests(issuer.PremiumPolicy); WorkspacePresentationConformanceSmoke.Verify(fixture);
                    Console.WriteLine("Presentation conformance: " + fixture.Name + " passed.");
                }
            }
            finally
            {
                foreach (var fixture in cases) { fixture.Profiles.Dispose(); fixture.Context.Dispose(); }
                foreach (var fixture in presentationCases) { fixture.Profiles.Dispose(); fixture.Context.Dispose(); }
                EditionAccess.SetForTests(previous); Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Verify(WorkspaceContextCase fixture)
        {
            var context = fixture.Context;
            var ancestor = context.GetType().BaseType;
            while (ancestor is not null && (!ancestor.IsGenericType || ancestor.GetGenericTypeDefinition() != typeof(GameWorkspaceContextBase<>))) ancestor = ancestor.BaseType;
            Require(ancestor is not null, fixture, "Native adapter implements an interface without inheriting the shared context algorithm.");
            var initialName = context.State.EditedName;
            Require(context.State.Open && !context.State.Dirty, fixture, "Native fixture must start at a saved baseline.");
            var notifications = 0; EventHandler changed = (_, _) => notifications++;
            context.StateChanged += changed;
            using (context.DeferRefresh()) using (context.DeferRefresh())
            { context.SetEditedName("One"); context.SetEditedName("Two"); Require(notifications == 0, fixture, "Nested refresh published an intermediate transition."); }
            Require(notifications == 1, fixture, "Common batching did not publish exactly one final state.");
            context.SetEditedName(initialName); Require(!context.State.Dirty, fixture, "Returning the edited name did not restore clean state.");
            var beforeSelection = context.CaptureSnapshot();
            context.Select(new("native:controller", "native:command", "native:slot", "native:alternative", null, "native:section"));
            Require(context.CaptureSnapshot() == beforeSelection && !context.State.Dirty, fixture, "Selection edited native data.");
            var scope = context.Context.Scope; var epoch = context.State.Identity.Generation;
            fixture.Replace(); Require(context.State.Identity.Generation > epoch && context.Context.Scope != scope, fixture, "Replacing the same native identity retained a stale scope.");
            fixture.Edit("Native edit"); context.SetEditedName(initialName + " edited"); var pending = context.CaptureSnapshot();
            try
            {
                Await(context.RunOperationAsync(WorkspaceOperationKind.Save, lease =>
                {
                    Require(context.Context.Busy && !context.State.Capabilities.Edit.Enabled && context.OwnsOperation(lease), fixture, "Operation was not shared with the assignment host.");
                    var nested = context.RunOperation(WorkspaceOperationKind.Discard, _ => throw new InvalidOperationException("Nested discard executed."));
                    Require(nested.Outcome == WorkspaceOperationOutcome.Blocked, fixture, "Nested entry bypassed the operation gate.");
                    var blocked = false; try { fixture.Edit("bypass"); } catch (InvalidOperationException) { blocked = true; }
                    Require(blocked, fixture, "A retained native session bypassed the save gate.");
                    throw new IOException("Synthetic persistence failure");
                }, default));
                throw new InvalidOperationException("Expected native persistence failure.");
            }
            catch (IOException) { }
            Require(context.CaptureSnapshot() == pending && context.State.Dirty && context.State.Operation is null, fixture, "Failed save lost a draft/name or retained the busy gate.");
            var events = notifications;
            var saved = Await(fixture.Profiles.SaveAsync(default));
            Require(fixture.PersistedName() == context.State.EditedName, fixture, "Native persisted baseline was not accepted.");
            Require(saved.Committed && !context.State.Dirty && notifications == events + 2, fixture, "Successful save did not establish one baseline with admission/release events.");
            fixture.Edit("Discard me");
            Require(Await(fixture.Profiles.DiscardAsync(default)).Outcome == WorkspaceOperationOutcome.Applied, fixture, "Shared discard was not applied.");
            Require(!context.State.Dirty, fixture, "Discard failed to restore the exact native baseline.");
            if (context.TryStartActivity(WorkspaceActivityKind.Test, out var input).Enabled)
            {
                var released = 0; input!.RegisterCleanup(() => released++);
                Require(!context.TryStartActivity(WorkspaceActivityKind.Scan, out _).Enabled, fixture, "A second activity was admitted.");
                context.StopActivity(); input.Dispose(); Require(released == 1, fixture, "Native activity resources were not released exactly once.");
            }
            Require(context.TryStartActivity(WorkspaceActivityKind.Scan, out var scan).Enabled, fixture, "Native scanning capability was not projected.");
            var rejected = context.RunOperation(WorkspaceOperationKind.Save, _ => throw new InvalidOperationException("Scan did not block Save."));
            Require(rejected.Outcome == WorkspaceOperationOutcome.Blocked, fixture, "Save bypassed the scan gate.");
            context.StopActivity(); Require(scan!.Cancellation.IsCancellationRequested, fixture, "Stopping a scan did not invalidate late native results.");
            fixture.MakeReadOnly(); Require(context.State.ReadOnly && !context.State.Capabilities.Save.Enabled, fixture, "Native readonly policy was not projected.");
            Require(context.RunOperation(WorkspaceOperationKind.Save, _ => throw new InvalidOperationException()).Outcome == WorkspaceOperationOutcome.Blocked, fixture, "Readonly save invoked native persistence.");
            context.StateChanged -= changed; var afterDetach = notifications; context.RefreshNativeState(); context.Dispose(); context.Dispose();
            Require(notifications == afterDetach && !context.State.Open, fixture, "Detached/disposed context still published notifications.");
            Console.WriteLine("Context conformance: " + fixture.Name + " passed.");
        }
        private static WorkspaceOperationResult Await(Task<WorkspaceOperationResult> operation)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!operation.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Native context operation did not complete.");
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            return operation.GetAwaiter().GetResult();
        }
        private static void Require(bool condition, WorkspaceContextCase fixture, string message)
        { if (!condition) throw new InvalidOperationException(fixture.Name + ": " + message); }
        #endregion END private helpers
    }
}
