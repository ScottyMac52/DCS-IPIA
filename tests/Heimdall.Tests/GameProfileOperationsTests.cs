using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Profile contract cases P01–P05 against the shared base through a typed native adapter.</summary>
    public sealed class GameProfileOperationsTests
    {
        private static (WorkspaceContextProbe Context, ProfileTestStore Store, ProfileTestInput Input, ProfileOperationsProbe Profiles) Open()
        {
            var context = new WorkspaceContextProbe(new()); context.Open();
            var store = new ProfileTestStore(); var input = new ProfileTestInput();
            return (context, store, input, new(context, store, input));
        }
        #region BEGIN Public
        /// <summary>P01: one admission, one durable write, readback equals candidate and baseline/name accepted only afterwards.</summary>
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "This conformance case deliberately dispatches through the common role contract.")]
        public async Task Dirty_save_writes_once_and_accepts_the_durable_candidate()
        {
            var (context, store, input, profiles) = Open();
            using (context) using (profiles)
            {
                IGameProfileOperations contract = profiles;
                context.Edit("staged"); context.SetEditedName("Renamed");
                var operations = new List<WorkspaceOperationKind?>();
                context.StateChanged += (_, _) => operations.Add(context.State.Operation);
                store.BeforeWrite = () => { Assert.Equal(WorkspaceOperationKind.Save, context.State.Operation); Assert.True(context.State.Dirty); return Task.CompletedTask; };
                var result = await contract.SaveAsync(CancellationToken.None);
                Assert.Equal(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "Probe profile saved.", true), result);
                Assert.Equal(1, store.Writes); Assert.Equal(1, profiles.Accepts); Assert.Equal(1, input.Stops); Assert.Equal(1, profiles.Refreshes);
                Assert.Equal(profiles.Candidate, store.Read("native:key"));
                Assert.Equal(new WorkspaceTestSnapshot("native:key", "Renamed", "staged"), context.Saved);
                Assert.False(context.State.Dirty); Assert.Equal("Renamed", context.State.EditedName);
                Assert.Null(context.State.Operation); Assert.Equal("Probe profile saved.", context.Status);
                Assert.Contains(WorkspaceOperationKind.Save, operations);
                Assert.Same(context, contract.Context);
            }
        }
        /// <summary>P02: clean writable saves report NoChange without writing; restricted/closed saves are Blocked with a reason.</summary>
        [Fact]
        public async Task Clean_readonly_closed_and_unsupported_saves_never_write_or_report_success()
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                var clean = await profiles.SaveAsync(CancellationToken.None);
                Assert.Equal(WorkspaceOperationOutcome.NoChange, clean.Outcome); Assert.False(clean.Committed);
                context.ReadOnly = true;
                var blockedClean = await profiles.SaveAsync(CancellationToken.None);
                context.Edit("staged");
                var blockedDirty = await profiles.SaveAsync(CancellationToken.None);
                Assert.Equal(WorkspaceOperationOutcome.Blocked, blockedClean.Outcome);
                Assert.Equal(WorkspaceOperationOutcome.Blocked, blockedDirty.Outcome);
                Assert.Equal("Native readonly policy.", blockedDirty.Message);
                // The native restriction reason must survive for clean saves too (EditionSmoke: reserved-hardware reason).
                Assert.Equal("Native readonly policy.", blockedClean.Message);
                context.ReadOnly = false; context.Supported = false; context.RefreshNativeState();
                Assert.Equal(WorkspaceOperationOutcome.Blocked, (await profiles.SaveAsync(CancellationToken.None)).Outcome);
                Assert.Equal(0, store.Writes); Assert.True(context.State.Dirty); Assert.NotEqual("Probe profile saved.", context.Status);
            }
            using var empty = new WorkspaceContextProbe(new());
            using var closed = new ProfileOperationsProbe(empty, store, new());
            Assert.Equal(WorkspaceOperationOutcome.Blocked, (await closed.SaveAsync(CancellationToken.None)).Outcome);
            Assert.Equal(WorkspaceOperationOutcome.Blocked, (await closed.DiscardAsync(CancellationToken.None)).Outcome);
        }
        /// <summary>P03: validation rejection, write failure and optimistic conflict retain draft/name/revision; retry uses the current candidate.</summary>
        [Theory]
        [InlineData("validation")]
        [InlineData("write")]
        [InlineData("conflict")]
        public async Task Failed_save_retains_pending_state_and_retry_writes_current_candidate(string failure)
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                context.Edit("first"); context.SetEditedName("Pending");
                if (failure == "validation") profiles.RejectCandidate = "Native name is invalid.";
                if (failure == "write") store.Fail = new IOException("Disk full.");
                if (failure == "conflict") store.Advance(new("native:key", "Other", "other"));
                var error = await Assert.ThrowsAnyAsync<Exception>(() => profiles.SaveAsync(CancellationToken.None));
                Assert.Equal(failure == "validation" ? "Native name is invalid." : failure == "write" ? "Disk full." : "The profile was changed by another window.", error.Message);
                Assert.Equal(0, store.Writes); Assert.Equal(0, profiles.Accepts); Assert.Equal(0, profiles.Refreshes);
                Assert.True(context.State.Dirty); Assert.Equal("Pending", context.State.EditedName); Assert.Equal("first", context.Working!.Data);
                Assert.Equal("saved", context.Saved!.Data); Assert.Equal(0, profiles.Revision); Assert.Null(context.State.Operation);
                Assert.NotEqual("Probe profile saved.", context.Status);
                profiles.RejectCandidate = null; store.Fail = null;
                if (failure == "conflict") return; // A conflicting writer requires the native reload path; pending state is retained above.
                context.Edit("second");
                Assert.Equal(WorkspaceOperationOutcome.Applied, (await profiles.SaveAsync(CancellationToken.None)).Outcome);
                Assert.Equal(new WorkspaceTestSnapshot("native:key", "Pending", "second"), store.Read("native:key"));
                Assert.Equal(1, store.Writes); Assert.Equal(1, profiles.Revision);
            }
        }
        /// <summary>P03/P04: cancellation before commit retains the workspace; cancellation after commit cannot undo a durable save.</summary>
        [Fact]
        public async Task Cancellation_before_commit_retains_and_after_commit_reports_committed()
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                context.Edit("staged");
                using var before = new CancellationTokenSource();
                store.BeforeWrite = () => { before.Cancel(); return Task.CompletedTask; };
                var cancelled = await profiles.SaveAsync(before.Token);
                Assert.Equal(WorkspaceOperationOutcome.Cancelled, cancelled.Outcome);
                Assert.Equal(0, store.Writes); Assert.True(context.State.Dirty); Assert.Null(context.State.Operation);
                using var after = new CancellationTokenSource();
                store.BeforeWrite = null; store.AfterCommit = after.Cancel;
                var committed = await profiles.SaveAsync(after.Token);
                Assert.Equal(WorkspaceOperationOutcome.Applied, committed.Outcome); Assert.True(committed.Committed);
                Assert.Equal(1, store.Writes); Assert.False(context.State.Dirty);
            }
        }
        /// <summary>P04: a post-commit refresh failure is a committed save with a warning, never a failure or second write.</summary>
        [Fact]
        public async Task Refresh_failure_after_commit_reports_committed_warning_once()
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                context.Edit("staged"); profiles.FailRefresh = true;
                var result = await profiles.SaveAsync(CancellationToken.None);
                Assert.Equal(WorkspaceOperationOutcome.Applied, result.Outcome); Assert.True(result.Committed);
                Assert.Contains("Probe profile saved.", result.Message, StringComparison.Ordinal);
                Assert.Contains("Panel refresh failed.", result.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("failed to save", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(1, store.Writes); Assert.False(context.State.Dirty); Assert.Null(context.State.Operation);
                Assert.Equal(result.Message, context.Status);
            }
        }
        /// <summary>Reentrant save/discard during persistence are blocked by the one context lease; close during save never installs into a disposed context.</summary>
        [Fact]
        public async Task Reentrant_entries_are_blocked_and_close_during_save_suppresses_installation()
        {
            var (context, store, _, profiles) = Open();
            using (profiles)
            {
                context.Edit("staged");
                WorkspaceOperationResult? nestedSave = null, nestedDiscard = null;
                store.BeforeWrite = async () =>
                {
                    nestedSave = await profiles.SaveAsync(CancellationToken.None);
                    nestedDiscard = await profiles.DiscardAsync(CancellationToken.None);
                };
                Assert.Equal(WorkspaceOperationOutcome.Applied, (await profiles.SaveAsync(CancellationToken.None)).Outcome);
                Assert.Equal(WorkspaceOperationOutcome.Blocked, nestedSave!.Outcome); Assert.Equal(WorkspaceOperationOutcome.Blocked, nestedDiscard!.Outcome);
                Assert.Equal(1, store.Writes); Assert.Equal(1, profiles.Accepts);
                context.Edit("closing");
                store.AfterCommit = context.Dispose;
                var closed = await profiles.SaveAsync(CancellationToken.None);
                Assert.True(closed.Committed); Assert.Equal(2, store.Writes); Assert.Equal(1, profiles.Accepts);
                Assert.Equal(1, context.Disposals);
            }
        }
        /// <summary>P05: discard restores the exact baseline/name without writing, invalidates targets, and a failed restore keeps the draft usable.</summary>
        [Fact]
        public async Task Discard_restores_without_writing_and_failed_restore_retains_draft()
        {
            var (context, store, input, profiles) = Open();
            using (context) using (profiles)
            {
                Assert.Equal(WorkspaceOperationOutcome.NoChange, (await profiles.DiscardAsync(CancellationToken.None)).Outcome);
                context.Edit("staged"); context.SetEditedName("Pending");
                profiles.FailRestore = true;
                await Assert.ThrowsAsync<InvalidOperationException>(() => profiles.DiscardAsync(CancellationToken.None));
                Assert.Equal("staged", context.Working!.Data); Assert.True(context.State.Dirty); Assert.Null(context.State.Operation);
                profiles.FailRestore = false; var generation = context.State.Identity.Generation;
                var result = await profiles.DiscardAsync(CancellationToken.None);
                Assert.Equal(new WorkspaceOperationResult(WorkspaceOperationOutcome.Applied, "Probe edits discarded."), result);
                Assert.Equal(context.Saved, context.Working); Assert.Equal("Profile", context.State.EditedName); Assert.False(context.State.Dirty);
                Assert.True(context.State.Identity.Generation > generation);
                Assert.Equal(0, store.Writes); Assert.Equal(2, input.Stops); Assert.Equal("Probe edits discarded.", context.Status);
            }
        }
        /// <summary>A clean read-only save during input activity is Blocked with the native reason, never NoChange, and runs no hook.</summary>
        [Fact]
        public async Task Clean_readonly_save_during_activity_keeps_native_reason_and_writes_nothing()
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                Assert.True(context.TryStartActivity(WorkspaceActivityKind.Test, out var activity).Enabled);
                using (activity)
                {
                    context.ReadOnly = true; context.RefreshNativeState();
                    var result = await profiles.SaveAsync(CancellationToken.None);
                    Assert.Equal(WorkspaceOperationOutcome.Blocked, result.Outcome);
                    Assert.Equal("Native readonly policy.", result.Message);
                    Assert.False(result.Committed); Assert.Equal(0, store.Writes); Assert.Equal(0, profiles.Accepts);
                }
            }
        }
        /// <summary>Smoke-risk audit: one save publishes exactly admission and release, even when accept reopens the native session and refresh re-reads state.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Save_publishes_exactly_admission_and_release(bool reopen)
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                profiles.ReopenOnAccept = reopen;
                context.Edit("staged"); context.SetEditedName("Renamed");
                var published = new List<WorkspaceOperationKind?>();
                context.StateChanged += (_, _) => published.Add(context.State.Operation);
                var result = await profiles.SaveAsync(CancellationToken.None);
                Assert.True(result.Committed); Assert.Equal(1, store.Writes);
                Assert.Equal([WorkspaceOperationKind.Save, null], published);
                Assert.False(context.State.Dirty); Assert.Equal("Renamed", context.State.EditedName);
            }
        }
        /// <summary>Smoke-risk audit: discard after a save restores exactly the post-save baseline (including the saved name) for both accept styles.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Discard_after_save_restores_the_exact_post_save_baseline(bool reopen)
        {
            var (context, store, _, profiles) = Open();
            using (context) using (profiles)
            {
                profiles.ReopenOnAccept = reopen;
                context.Edit("staged"); context.SetEditedName("Renamed");
                await profiles.SaveAsync(CancellationToken.None);
                var baseline = context.Working; var fingerprint = context.CaptureSnapshot();
                context.Edit("discard me"); context.SetEditedName("Other");
                Assert.Equal(WorkspaceOperationOutcome.Applied, (await profiles.DiscardAsync(CancellationToken.None)).Outcome);
                Assert.Equal(baseline, context.Working); Assert.Equal(fingerprint, context.CaptureSnapshot());
                Assert.Equal(store.Read("native:key"), context.Working); Assert.Equal(1, store.Writes);
            }
        }
        #endregion END Public
    }
}
