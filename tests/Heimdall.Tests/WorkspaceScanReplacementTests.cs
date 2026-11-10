using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Hardware rescans replace obsolete evidence requests through the shared activity owner.</summary>
    public sealed class WorkspaceScanReplacementTests
    {
        #region BEGIN Public
        /// <summary>Replaces a scan lease without cancelling its successor or publishing an idle transition.</summary>
        [Fact]
        public void Replacing_a_scan_cancels_only_the_old_lease_without_publishing_idle()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var snapshot = context.CaptureSnapshot();
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var old).Enabled);
            var released = 0; WorkspaceActivityKind? duringCleanup = null; WorkspaceOperationOutcome? saveOutcome = null;
            old!.RegisterCleanup(() =>
            {
                released++;
                duringCleanup = context.State.Activity;
                saveOutcome = context.RunOperation(WorkspaceOperationKind.Save, _ => throw new InvalidOperationException("Unexpected save")).Outcome;
            });
            var states = new List<WorkspaceActivityKind?>();
            context.StateChanged += (_, _) => states.Add(context.State.Activity);
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var current).Enabled);
            Assert.True(old.Cancellation.IsCancellationRequested); Assert.False(current!.Cancellation.IsCancellationRequested);
            Assert.Equal(1, released); Assert.DoesNotContain((WorkspaceActivityKind?)null, states);
            Assert.Equal(WorkspaceActivityKind.Scan, duringCleanup); Assert.Equal(WorkspaceOperationOutcome.Blocked, saveOutcome);
            old.Dispose(); Assert.Equal(WorkspaceActivityKind.Scan, context.State.Activity);
            Assert.False(context.TryStartActivity(WorkspaceActivityKind.Test, out _).Enabled);
            current.Dispose(); Assert.Null(context.State.Activity); Assert.Equal(1, released);
            Assert.Equal(snapshot, context.CaptureSnapshot()); Assert.False(context.State.Dirty);
        }

        /// <summary>Retains the current scan when a replacement cannot be admitted.</summary>
        [Fact]
        public void Unsupported_replacement_preserves_the_running_scan()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var old).Enabled);
            context.Supported = false;
            Assert.False(context.TryStartActivity(WorkspaceActivityKind.Scan, out var rejected).Enabled);
            Assert.Null(rejected); Assert.False(old!.Cancellation.IsCancellationRequested);
            Assert.Equal(WorkspaceActivityKind.Scan, context.State.Activity);
        }

        /// <summary>Prevents scanning from replacing an unrelated input activity.</summary>
        [Theory]
        [InlineData(WorkspaceActivityKind.Test)]
        [InlineData(WorkspaceActivityKind.Identify)]
        public void Rescanning_does_not_replace_other_input_activities(WorkspaceActivityKind kind)
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            Assert.True(context.TryStartActivity(kind, out var activity).Enabled);
            Assert.False(context.TryStartActivity(WorkspaceActivityKind.Scan, out _).Enabled);
            Assert.False(activity!.Cancellation.IsCancellationRequested); Assert.Equal(kind, context.State.Activity);
        }

        /// <summary>Rejects stale scan results and failures after a newer scan owns the evidence.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Older_completion_or_failure_cannot_replace_newer_evidence_or_clear_a_newer_scan(bool fail)
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var snapshot = context.CaptureSnapshot(); var evidence = "";
            async Task Scan(Task<string> pending)
            {
                Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var lease).Enabled);
                using var scan = lease!;
                try
                {
                    var result = await pending;
                    if (!scan.Cancellation.IsCancellationRequested && scan.Identity == context.State.Identity) evidence = result;
                }
                catch (IOException) when (scan.Cancellation.IsCancellationRequested) { }
            }
            var first = new TaskCompletionSource<string>(); var third = new TaskCompletionSource<string>();
            var old = Scan(first.Task);
            await Scan(Task.FromResult("new capabilities"));
            Assert.Equal("new capabilities", evidence); Assert.Null(context.State.Activity);
            var current = Scan(third.Task);
            if (fail) first.SetException(new IOException("obsolete scan failure")); else first.SetResult("obsolete capabilities");
            await old;
            Assert.Equal("new capabilities", evidence); Assert.Equal(WorkspaceActivityKind.Scan, context.State.Activity);
            third.SetResult("latest capabilities"); await current;
            Assert.Equal("latest capabilities", evidence); Assert.Null(context.State.Activity);
            Assert.Equal(snapshot, context.CaptureSnapshot()); Assert.False(context.State.Dirty);
        }

        /// <summary>Cancels scan ownership on close and tolerates late disposal of an obsolete lease.</summary>
        [Fact]
        public void Closing_cancels_the_replacement_and_late_old_disposal_is_harmless()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var old).Enabled);
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var current).Enabled);
            context.Dispose(); var closed = context.State;
            Assert.True(old!.Cancellation.IsCancellationRequested); Assert.True(current!.Cancellation.IsCancellationRequested);
            old.Dispose(); current.Dispose(); Assert.Equal(closed, context.State);
            Assert.Null(context.State.Activity); Assert.False(context.State.Open);
        }
        #endregion END Public
    }
}
