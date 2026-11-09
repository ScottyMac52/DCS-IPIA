using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Pins shared browser invalidation, retained identity and publication behavior independently of native formats.</summary>
    public sealed class AssignmentBrowserContractTests
    {
        private static readonly string[] expectedPitch = ["pitch"];
        #region BEGIN Public
        /// <summary>Native UI observers receive selection only after its matching rows have been published.</summary>
        [Fact]
        public void Command_selection_publishes_rows_before_notifying_native_editors()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            using var browser = new AssignmentBrowserProbe(context); browser.Refresh();
            var observed = new List<(string? Command, string[] Records)>();
            context.StateChanged += (_, _) => observed.Add((context.State.Selection.CommandId, browser.Records.ToArray()));
            browser.SelectCommand("fire");
            browser.SelectCommand("pitch");
            Assert.NotEmpty(observed);
            Assert.All(observed, item => Assert.Equal(new[] { item.Command }, item.Records));
            Assert.Equal("pitch", context.State.Selection.CommandId);
        }
              

        /// <summary>An explicit native command survives a refresh deferred until persistence releases the context.</summary>
        [Fact]
        public async Task Preferred_command_survives_a_refresh_deferred_by_persistence()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            using var browser = new AssignmentBrowserProbe(context); browser.Refresh(); browser.SelectCommand("fire");
            var pending = new TaskCompletionSource<WorkspaceOperationResult>();
            var running = context.RunOperationAsync(WorkspaceOperationKind.Save, _ => pending.Task, default);
            browser.Query("all", "", "pitch");
            Assert.Equal("fire", context.State.Selection.CommandId);
            pending.SetResult(new(WorkspaceOperationOutcome.NoChange, "")); await running;
            Assert.Equal("pitch", context.State.Selection.CommandId);
            Assert.Equal(expectedPitch, browser.Records);
        }
        /// <summary>A warm query reuses content and an unchanged projection is not republished.</summary>
        [Fact]
        public void Warm_search_reuses_content_and_does_not_republish_identical_results()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            using var browser = new AssignmentBrowserProbe(context);
            browser.Refresh(); var initial = browser.Metrics;
            browser.Search("  fire  ");
            Assert.Single(browser.Commands); Assert.Equal("fire", browser.Commands[0]);
            var filtered = browser.Metrics; browser.Search("fire"); browser.Refresh();
            Assert.Equal(initial.ContentBuilds, browser.Metrics.ContentBuilds);
            Assert.Equal(filtered.Publications, browser.Metrics.Publications);
            browser.SelectCommand("fire"); browser.SelectCommand("missing");
            Assert.Null(context.State.Selection.CommandId); Assert.Null(context.State.Selection.InputId);
        }
        /// <summary>Native failures retain both published collections, clear unsafe targets and can be retried.</summary>
        [Fact]
        public void Projection_failure_retains_rows_and_retry_recovers()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            using var browser = new AssignmentBrowserProbe(context); browser.Refresh(); browser.SelectCommand("fire");
            var commands = browser.Commands; var records = browser.Records;
            browser.Fail = true; context.Edit("changed");
            Assert.Same(commands, browser.Commands); Assert.Same(records, browser.Records);
            Assert.Null(context.State.Selection.CommandId); Assert.Contains("native projection failed", context.Status, StringComparison.Ordinal);
            browser.Fail = false; browser.Refresh(); Assert.NotEmpty(browser.Commands);
        }
        /// <summary>Nested identical refreshes coalesce; input-only notifications do not rebuild or republish the browser.</summary>
        [Fact]
        public async Task Reentrant_refresh_and_input_changes_do_not_multiply_publications()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            using var browser = new AssignmentBrowserProbe(context); browser.Published = () => browser.Refresh();
            browser.Refresh(); Assert.Equal(1, browser.Metrics.ContentBuilds); Assert.Equal(1, browser.Metrics.Publications);
            browser.SelectCommand("fire"); var previous = browser.Metrics; var commands = browser.Commands;
            context.Select(context.State.Selection with { InputId = "button" });
            Assert.Equal(previous, browser.Metrics); Assert.Same(commands, browser.Commands);
            var pending = new TaskCompletionSource<WorkspaceOperationResult>();
            var operation = context.RunOperationAsync(WorkspaceOperationKind.Save, _ => pending.Task, default);
            browser.SelectCommand("pitch"); Assert.Equal("fire", context.State.Selection.CommandId);
            pending.SetResult(new(WorkspaceOperationOutcome.NoChange, "")); await operation;
            browser.Dispose(); context.Edit("after disposal"); Assert.Equal(previous, browser.Metrics);
        }
        /// <summary>A filter typed during persistence runs after release without admitting a stale selection.</summary>
        [Fact]
        public async Task Query_during_busy_operation_is_deferred_until_release()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            using var browser = new AssignmentBrowserProbe(context); browser.Refresh(); browser.SelectCommand("fire");
            var pending = new TaskCompletionSource<WorkspaceOperationResult>();
            var running = context.RunOperationAsync(WorkspaceOperationKind.Save, _ => pending.Task, default);
            browser.Search("pitch"); Assert.Equal(2, browser.Commands.Count); Assert.Equal("fire", context.State.Selection.CommandId);
            pending.SetResult(new(WorkspaceOperationOutcome.NoChange, "")); await running;
            Assert.Equal("pitch", Assert.Single(browser.Commands)); Assert.Null(context.State.Selection.CommandId);
            Assert.Equal(1, browser.Metrics.ContentBuilds);
        }
        #endregion END Public
    }
}

