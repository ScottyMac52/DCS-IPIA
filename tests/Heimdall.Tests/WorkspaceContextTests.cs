using Heimdall.Application;
using Heimdall.Application.Games;
using System.Diagnostics.CodeAnalysis;
using Heimdall.Application.Interfaces;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises shared state, stale review, operation, notification and resource contracts before native integration.</summary>
    public sealed class WorkspaceContextTests
    {
        #region BEGIN Public
        /// <summary>Selection and name overlays have distinct dirty semantics; native content changes remain visible.</summary>
        [Fact]
        [SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "This conformance case deliberately dispatches through the common role contract.")]
        public void Selection_name_native_change_and_same_key_replacement_have_one_owner()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            IGameWorkspaceContext contract = context;
            Assert.False(contract.State.Dirty);
            contract.Select(new("controller", "command", "native-slot", null, null, "section"));
            Assert.False(contract.State.Dirty);
            var fingerprint = contract.CaptureSnapshot(); var epoch = contract.State.Identity.Generation;
            contract.SetEditedName("Renamed"); Assert.True(contract.State.Dirty);
            Assert.NotEqual(fingerprint, contract.CaptureSnapshot());
            contract.SetEditedName("Profile"); Assert.False(contract.State.Dirty);
            context.Edit("one"); var version = contract.State.Version;
            context.Edit("two"); Assert.True(contract.State.Version > version);
            context.Open(); Assert.True(contract.State.Identity.Generation > epoch);
            Assert.Null(contract.State.Selection.CommandId); Assert.False(contract.State.Dirty);
        }
        /// <summary>Nested refresh scopes publish one final notification; status is independent and listener failures are isolated.</summary>
        [Fact]
        public void Nested_refresh_and_notifications_are_distinct_and_nonrecursive()
        {
            var scheduler = new WorkspaceTestScheduler(); using var context = new WorkspaceContextProbe(scheduler); context.Open();
            var changes = 0; var status = 0;
            context.StateChanged += (_, _) => throw new InvalidOperationException("Synthetic listener failure");
            context.StateChanged += (_, _) => { changes++; context.RefreshNativeState(); };
            context.StatusChanged += (_, _) => throw new InvalidOperationException("Synthetic status failure");
            context.StatusChanged += (_, _) => status++;
            using (context.DeferRefresh()) using (context.DeferRefresh()) { context.Edit("first"); context.Edit("second"); Assert.Equal(0, changes); }
            scheduler.Drain(); Assert.Equal(1, changes);
            context.RefreshNativeState(); Assert.Equal(1, changes);
            context.ReportStatus("Ready"); context.ReportStatus("Ready"); Assert.Equal(1, status);
        }
        /// <summary>Every failure releases the single operation gate and leaves native draft/name/baseline available for retry.</summary>
        [Fact]
        public async Task Failed_and_reentrant_save_preserve_native_state_and_release_busy()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open(); context.Edit("pending"); context.SetEditedName("Edited");
            var before = context.CaptureSnapshot(); var saved = context.Saved; var events = new List<WorkspaceOperationKind?>();
            context.StateChanged += (_, _) => events.Add(context.State.Operation);
            await Assert.ThrowsAsync<IOException>(() => context.RunOperationAsync(WorkspaceOperationKind.Save, async lease =>
            {
                Assert.Equal(WorkspaceOperationKind.Save, context.State.Operation); Assert.True(context.Context.Busy);
                Assert.False(context.State.Capabilities.Edit.Enabled);
                var nested = await context.RunOperationAsync(WorkspaceOperationKind.Save, _ => throw new InvalidOperationException("Must not execute"), default);
                Assert.Equal(WorkspaceOperationOutcome.Blocked, nested.Outcome);
                Assert.Throws<InvalidOperationException>(() => context.SetEditedName("Lost"));
                Assert.Throws<InvalidOperationException>(() => context.Select(new(null, null, null, null, null, "")));
                throw new IOException("Synthetic store failure");
            }, default));
            Assert.Equal(before, context.CaptureSnapshot()); Assert.Same(saved, context.Saved); Assert.Null(context.State.Operation);
            Assert.Equal(new WorkspaceOperationKind?[] { WorkspaceOperationKind.Save, null }, events);
            var retry = await context.RunOperationAsync(WorkspaceOperationKind.Save, _ => { context.Accept(); return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "Saved", true)); }, default);
            Assert.True(retry.Committed); Assert.Equal("Edited", context.State.EditedName);
        }
        /// <summary>Cancellation before admission and during an action preserves native data and releases the operation.</summary>
        [Fact]
        public async Task Cancellation_never_runs_precancelled_actions_or_loses_the_workspace()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open(); context.Edit("pending");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var result = await context.RunOperationAsync(WorkspaceOperationKind.Save, _ => throw new InvalidOperationException("Must not execute"), cancelled.Token);
            Assert.Equal(WorkspaceOperationOutcome.Cancelled, result.Outcome);
            using var active = new CancellationTokenSource();
            result = await context.RunOperationAsync(WorkspaceOperationKind.Save, lease => { active.Cancel(); lease.Cancellation.ThrowIfCancellationRequested(); return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.NoChange, "")); }, active.Token);
            Assert.Equal(WorkspaceOperationOutcome.Cancelled, result.Outcome); Assert.True(context.State.Dirty); Assert.Null(context.State.Operation);
        }
        /// <summary>Activity leases release resources once, stop before saves, and cannot let stale disposal clear a new lease.</summary>
        [Fact]
        public async Task Activities_are_exclusive_cancelled_and_generation_bound()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Test, out var first).Enabled);
            var releases = 0; first!.RegisterCleanup(() => releases++); first.RegisterCleanup(() => throw new IOException("cleanup"));
            Assert.True(context.Context.Testing); Assert.False(context.TryStartActivity(WorkspaceActivityKind.Identify, out _).Enabled);
            var saved = await context.RunOperationAsync(WorkspaceOperationKind.Save, _ => { Assert.Equal(1, releases); return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.NoChange, "")); }, default);
            Assert.Equal(WorkspaceOperationOutcome.NoChange, saved.Outcome); Assert.True(first.Cancellation.IsCancellationRequested);
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Identify, out var second).Enabled);
            first.Dispose(); Assert.Equal(WorkspaceActivityKind.Identify, context.State.Activity);
            context.StopActivity(); Assert.True(second!.Cancellation.IsCancellationRequested);
            first.RegisterCleanup(() => releases++); Assert.Equal(2, releases);
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var scan).Enabled);
            var blocked = await context.RunOperationAsync(WorkspaceOperationKind.Save, _ => throw new IOException(), default);
            Assert.Equal(WorkspaceOperationOutcome.Blocked, blocked.Outcome);
            var epoch = scan!.Identity; context.StopActivity(); context.Open(); Assert.NotEqual(epoch, context.State.Identity);
        }
        /// <summary>Readonly and unsupported capabilities have explicit reasons while restoration may remain available.</summary>
        [Theory]
        [InlineData(WorkspaceOperationKind.Save)][InlineData(WorkspaceOperationKind.NativeEdit)][InlineData(WorkspaceOperationKind.ControllerChange)]
        public async Task Readonly_operations_are_rejected_before_native_action(WorkspaceOperationKind kind)
        {
            using var context = new WorkspaceContextProbe(new()); context.Open(); context.Edit("retained"); context.ReadOnly = true; context.RefreshNativeState();
            Assert.True(context.State.Dirty); Assert.True(context.Context.ReadOnly);
            var result = await context.RunOperationAsync(kind, _ => throw new IOException(), default);
            Assert.Equal(WorkspaceOperationOutcome.Blocked, result.Outcome); Assert.Equal("Native readonly policy.", result.Message);
            Assert.False(context.State.Capabilities.Edit.Enabled);
            Assert.True(context.State.Capabilities.Discard.Enabled);
        }
        /// <summary>Unsupported and empty sessions never invoke blocked hooks; import can install into an empty context.</summary>
        [Fact]
        public async Task Unsupported_closed_and_invalid_requests_are_explicit()
        {
            using var context = new WorkspaceContextProbe(new());
            var blocked = await context.RunOperationAsync(WorkspaceOperationKind.Export, _ => throw new IOException(), default);
            Assert.Equal(WorkspaceOperationOutcome.Blocked, blocked.Outcome);
            await context.RunOperationAsync(WorkspaceOperationKind.Import, _ => { context.Open(); return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "")); }, default);
            context.Supported = false; context.RefreshNativeState();
            foreach (var activity in Enum.GetValues<WorkspaceActivityKind>()) Assert.False(context.TryStartActivity(activity, out _).Enabled);
            foreach (var kind in new[] { WorkspaceOperationKind.Save, WorkspaceOperationKind.Discard, WorkspaceOperationKind.NativeEdit, WorkspaceOperationKind.ControllerChange })
                Assert.Equal(WorkspaceOperationOutcome.Blocked, (await context.RunOperationAsync(kind, _ => throw new IOException(), default)).Outcome);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.TryStartActivity((WorkspaceActivityKind)99, out _));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.RunOperationAsync((WorkspaceOperationKind)99, _ => throw new IOException(), default));
        }
        /// <summary>Disposal cancels active work, suppresses UI publication, and defers native destruction until a write finishes.</summary>
        [Fact]
        public async Task Dispose_during_commit_retains_the_receipt_and_releases_native_once()
        {
            var context = new WorkspaceContextProbe(new()); context.Open(); var states = 0; context.StateChanged += (_, _) => states++;
            var result = await context.RunOperationAsync(WorkspaceOperationKind.Save, lease =>
            {
                context.Dispose(); Assert.True(lease.Cancellation.IsCancellationRequested); Assert.Equal(0, context.Disposals);
                context.Accept(); return Task.FromResult(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "Committed", true));
            }, default);
            Assert.True(result.Committed); Assert.Equal(1, context.Disposals); Assert.False(context.State.Open);
            context.Dispose(); context.ReportStatus("ignored"); context.RefreshNativeState(); Assert.Equal(1, states);
            Assert.False(context.TryStartActivity(WorkspaceActivityKind.Scan, out _).Enabled);
            Assert.Equal(WorkspaceOperationOutcome.Blocked, (await context.RunOperationAsync(WorkspaceOperationKind.Save, _ => throw new IOException(), default)).Outcome);
            Assert.Throws<ObjectDisposedException>(() => context.DeferRefresh());
        }
        /// <summary>Scope encoding distinguishes delimiter-bearing native IDs and old targets are rejected by the existing workflow.</summary>
        [Fact]
        public void Native_scope_and_actual_snapshot_guard_the_existing_engine()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open(); context.Select(new("a:b", "command", "slot", "alt", null, "c"));
            var original = context.Context.Scope;
            context.Select(new("a", "command", "slot", "alt", null, "b:c")); Assert.NotEqual(original, context.Context.Scope);
            var result = context.AssignmentWorkflow.Execute(context, new WorkspaceAssignmentTarget(original, "command", "input", "alt"), () => new(true, true, ""), () => throw new IOException());
            Assert.Equal(DraftOutcome.Blocked, result.Outcome);
            var version = context.State.Version; context.Restore(); Assert.True(context.State.Version > version);
        }
        /// <summary>Controller, scope, command and slot changes cannot carry an old pending physical input.</summary>
        [Fact]
        public void Common_selection_clears_dependent_targets()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            context.Select(new("controller", "command", "slot", null, null, "scope"));
            context.Select(context.State.Selection with { InputId = "JOY_BTN1" });
            Assert.Equal("JOY_BTN1", context.State.Selection.InputId);
            context.Select(context.State.Selection with { CommandId = "another" });
            Assert.Null(context.State.Selection.InputId);
            context.Select(context.State.Selection with { AlternativeId = "record" });
            context.Select(context.State.Selection with { NativeScope = "other scope" });
            Assert.Null(context.State.Selection.AlternativeId);
        }
        /// <summary>Status callbacks cannot admit nested actions; state callbacks are posted rather than recursively published.</summary>
        [Fact]
        public void Publication_and_owner_thread_are_enforced()
        {
            var scheduler = new WorkspaceTestScheduler(); using var context = new WorkspaceContextProbe(scheduler); context.Open();
            WorkspaceOperationResult? nested = null;
            context.StatusChanged += (_, _) => nested = context.RunOperation(WorkspaceOperationKind.Save, _ => throw new IOException());
            context.ReportStatus("Changed"); Assert.Equal(WorkspaceOperationOutcome.Blocked, nested!.Outcome);
            var count = 0; context.StateChanged += (_, _) => { count++; if (count == 1) context.SetEditedName("Posted"); };
            context.SetEditedName("First"); Assert.Equal(1, count); scheduler.Drain(); Assert.Equal(2, count);
            scheduler.HasAccess = false;
            Assert.Throws<InvalidOperationException>(context.RefreshNativeState); scheduler.HasAccess = true;
        }
        /// <summary>A second context reopening the same native workspace cannot reuse an earlier window target scope.</summary>
        [Fact]
        public void Reopening_the_same_key_in_another_context_uses_a_new_epoch()
        {
            using var first = new WorkspaceContextProbe(new()); using var second = new WorkspaceContextProbe(new());
            first.Open("same"); second.Open("same");
            Assert.Equal(first.State.Identity.Key, second.State.Identity.Key);
            Assert.NotEqual(first.State.Identity.Generation, second.State.Identity.Generation);
            Assert.NotEqual(first.Context.Scope, second.Context.Scope);
        }
        /// <summary>Stopping native input cannot reenter persistence before the outer save reserves its lease.</summary>
        [Fact]
        public void Input_cleanup_cannot_reenter_an_admitted_operation()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            context.TryStartActivity(WorkspaceActivityKind.Test, out var activity);
            WorkspaceOperationResult? nested = null; var writes = 0;
            activity!.RegisterCleanup(() => nested = context.RunOperation(WorkspaceOperationKind.Save, _ => { writes++; return new(WorkspaceOperationOutcome.Applied, ""); }));
            context.RunOperation(WorkspaceOperationKind.Save, _ => { writes++; return new(WorkspaceOperationOutcome.Applied, ""); });
            Assert.Equal(WorkspaceOperationOutcome.Blocked, nested!.Outcome); Assert.Equal(1, writes);
        }
        /// <summary>Native ID refresh preserves valid commands and invalidates pending input without requiring SimCommand rows.</summary>
        [Fact]
        public void Native_id_refresh_handles_empty_filtered_and_changed_controller_projections()
        {
            var selection = new GameAssignmentSelection("");
            selection.RefreshIds("one", ["a", "b"]); Assert.Equal("a", selection.CommandId);
            selection.SelectInput("button"); selection.RefreshIds("one", ["a", "b"], "b");
            Assert.Equal("b", selection.CommandId); Assert.Null(selection.InputKey);
            selection.SelectInput("pending"); selection.RefreshIds("two", ["a", "b"]);
            Assert.Equal("b", selection.CommandId); Assert.Null(selection.InputKey);
            selection.RefreshIds("two", ["c"], selectFirst: false); Assert.Null(selection.CommandId);
            selection.RefreshIds("two", ["c"]); Assert.Equal("c", selection.CommandId);
            selection.RefreshIds(null, []); Assert.Null(selection.CommandId); Assert.Null(selection.ControllerId);
        }
        #endregion END Public
    }
}
