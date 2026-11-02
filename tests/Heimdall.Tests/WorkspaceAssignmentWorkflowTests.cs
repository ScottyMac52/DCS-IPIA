using Heimdall.Application;
using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises shared assignment policy independently of UI and native file schemas.</summary>
    public sealed class WorkspaceAssignmentWorkflowTests
    {
        #region BEGIN Public
        /// <summary>All consumers receive the same gate precedence and cannot call native mutation when unavailable.</summary>
        [Theory]
        [InlineData(false, false, false, false, "Open")]
        [InlineData(true, true, true, true, "read-only")]
        [InlineData(true, false, true, true, "Stop input")]
        [InlineData(true, false, true, false, "Finish")]
        public void Unavailable_states_do_not_review_or_mutate(bool open, bool readOnly, bool busy, bool testing, string reason)
        {
            var workflow = new WorkspaceAssignmentWorkflow(); var completed = 0;
            workflow.Completed += (_, result) => { Assert.Equal(DraftOutcome.Blocked, result.Outcome); completed++; };
            var host = new WorkspaceAssignmentHost(() => new("scope", open, readOnly, busy, testing), () => "original");
            var operation = new WorkspaceAssignmentOperation(Target(), () => throw new InvalidOperationException("review called"),
                () => throw new InvalidOperationException("mutation called"));
            var result = workflow.Execute(host, operation);
            Assert.Equal(DraftOutcome.Blocked, result.Outcome); Assert.Contains(reason, result.Message, StringComparison.Ordinal);
            Assert.Null(workflow.Selection); Assert.Equal(1, completed);
        }
        /// <summary>A command dropped onto a stale profile, controller or context never reaches its adapter.</summary>
        [Theory]
        [InlineData("other")]
        [InlineData("")]
        [InlineData(" ")]
        public void Stale_targets_are_blocked(string scope)
        {
            var workflow = new WorkspaceAssignmentWorkflow();
            var result = workflow.Execute(new WorkspaceAssignmentHost(Ready, () => "before"),
                new WorkspaceAssignmentOperation(Target() with { Scope = scope }, () => throw new InvalidOperationException(),
                    () => throw new InvalidOperationException()));
            Assert.Equal(DraftOutcome.Blocked, result.Outcome); Assert.Null(workflow.Selection);
        }
        /// <summary>Modal review must not commit an old selection or overwrite a draft changed by another action.</summary>
        [Theory]
        [InlineData("scope")]
        [InlineData("draft")]
        [InlineData("testing")]
        [InlineData("busy")]
        [InlineData("closed")]
        [InlineData("readonly")]
        public void State_is_rechecked_after_review(string change)
        {
            var context = Ready(); var native = "before";
            var host = new WorkspaceAssignmentHost(() => context, () => native);
            var operation = new WorkspaceAssignmentOperation(Target(), () =>
            {
                context = change switch
                {
                    "scope" => context with { Scope = "new controller" },
                    "testing" => context with { Testing = true },
                    "busy" => context with { Busy = true },
                    "closed" => context with { Open = false },
                    "readonly" => context with { ReadOnly = true },
                    _ => context
                };
                if (change == "draft") native = "external edit";
                return new(true, true, "");
            }, () => throw new InvalidOperationException("stale mutation called"));
            Assert.Equal(DraftOutcome.Blocked, new WorkspaceAssignmentWorkflow().Execute(host, operation).Outcome);
        }
        /// <summary>Adapter rejection and semantic no-op are handled before native commit.</summary>
        [Theory]
        [InlineData(false, false, DraftOutcome.Blocked)]
        [InlineData(true, false, DraftOutcome.NoChange)]
        public void Review_can_reject_or_skip_the_action(bool valid, bool changed, DraftOutcome expected)
        {
            var result = new WorkspaceAssignmentWorkflow().Execute(new WorkspaceAssignmentHost(Ready, () => "same"),
                new WorkspaceAssignmentOperation(Target(), () => new(valid, changed, "native reason"),
                    () => throw new InvalidOperationException("mutation called")));
            Assert.Equal(expected, result.Outcome);
            Assert.Equal(valid ? "Assignment unchanged." : "native reason", result.Message);
        }
        /// <summary>Feedback reflects the actual draft and completion fires once for all normal outcomes.</summary>
        [Theory]
        [InlineData(true, "", DraftOutcome.Applied)]
        [InlineData(true, "adapter feedback", DraftOutcome.Applied)]
        [InlineData(false, "claimed change", DraftOutcome.NoChange)]
        public void Applied_feedback_requires_an_actual_snapshot_change(bool changes, string message, DraftOutcome expected)
        {
            var native = "before"; var completed = 0; var workflow = new WorkspaceAssignmentWorkflow();
            var target = Target();
            workflow.Completed += (_, result) => { completed++; Assert.Equal(expected, result.Outcome); };
            var result = workflow.Execute(new WorkspaceAssignmentHost(Ready, () => native),
                new WorkspaceAssignmentOperation(target, () => new(true, true, ""), () =>
                {
                    if (changes) native = "after";
                    return new(DraftOutcome.Applied, message);
                }));
            Assert.Equal(expected, result.Outcome); Assert.Equal(1, completed); Assert.Equal(target, workflow.Selection);
            Assert.Equal(!changes ? "Assignment unchanged." : message.Length == 0
                ? "Assignment changed and staged. Save before exporting." : message, result.Message);
        }
        /// <summary>Native conflict, cancellation and no-change outcomes retain their explanations and operation records.</summary>
        [Theory]
        [InlineData(DraftOutcome.Blocked)]
        [InlineData(DraftOutcome.NoChange)]
        [InlineData(DraftOutcome.ConfirmationRequired)]
        public void Adapter_results_are_preserved(DraftOutcome outcome)
        {
            var expected = new DraftResult(outcome, "native outcome");
            var workflow = new WorkspaceAssignmentWorkflow();
            var actual = workflow.Execute(new WorkspaceAssignmentHost(Ready, () => "same"),
                new WorkspaceAssignmentOperation(Target(), () => new(true, true, ""), () => expected));
            Assert.Same(expected, actual);
            if (outcome == DraftOutcome.Blocked) Assert.Null(workflow.Selection);
        }
        /// <summary>State changes caused by a successful commit invalidate the old selection.</summary>
        [Fact]
        public void Completed_commit_does_not_retain_a_stale_controller_selection()
        {
            var context = Ready(); var native = "before"; var workflow = new WorkspaceAssignmentWorkflow();
            var result = workflow.Execute(new WorkspaceAssignmentHost(() => context, () => native),
                new WorkspaceAssignmentOperation(Target(), () => new(true, true, ""), () =>
                {
                    context = context with { Scope = "new selection" }; native = "after";
                    return new(DraftOutcome.Applied, "done");
                }));
            Assert.Equal(DraftOutcome.Applied, result.Outcome); Assert.Null(workflow.Selection);
        }
        /// <summary>Callbacks cannot recursively apply a second edit while the first is in flight.</summary>
        [Fact]
        public void Reentrancy_is_blocked_without_a_second_completion()
        {
            var workflow = new WorkspaceAssignmentWorkflow(); var host = new WorkspaceAssignmentHost(Ready, () => "same");
            var completions = 0;
            var nested = new WorkspaceAssignmentOperation(Target(), () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
            workflow.Completed += (_, _) =>
            {
                completions++;
                Assert.Equal(DraftOutcome.Blocked, workflow.Execute(host, nested).Outcome);
            };
            var operation = new WorkspaceAssignmentOperation(Target(), () =>
            {
                Assert.Equal(DraftOutcome.Blocked, workflow.Execute(host, nested).Outcome);
                return new(true, false, "");
            }, () => throw new InvalidOperationException());
            Assert.Equal(DraftOutcome.NoChange, workflow.Execute(host, operation).Outcome); Assert.Equal(1, completions);
        }
        /// <summary>Failed review or atomic native commit releases the execution guard for a subsequent valid action.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Adapter_exceptions_do_not_leave_the_workflow_busy(bool failReview)
        {
            var workflow = new WorkspaceAssignmentWorkflow(); var host = new WorkspaceAssignmentHost(Ready, () => "same");
            Assert.Throws<InvalidOperationException>(() => workflow.Execute(host,
                new WorkspaceAssignmentOperation(Target(), () => failReview ? throw new InvalidOperationException() : new(true, true, ""),
                    () => throw new InvalidOperationException())));
            Assert.Equal(DraftOutcome.NoChange, workflow.Execute(host,
                new WorkspaceAssignmentOperation(Target(), () => new(true, false, ""), () => throw new InvalidOperationException())).Outcome);
        }
        /// <summary>Missing required collaborators fail explicitly.</summary>
        [Fact]
        public void Null_arguments_are_rejected()
        {
            var workflow = new WorkspaceAssignmentWorkflow();
            var operation = new WorkspaceAssignmentOperation(Target(), () => new(true, false, ""), () => throw new InvalidOperationException());
            Assert.Throws<ArgumentNullException>(() => workflow.Execute(null!, operation));
            Assert.Throws<ArgumentNullException>(() => workflow.Execute(new WorkspaceAssignmentHost(Ready, () => ""), null!));
        }
        /// <summary>Targets and contexts retain all scope values through immutable copies and value equality.</summary>
        [Fact]
        public void Value_contracts_preserve_the_complete_target_and_context()
        {
            var target = Target(); var copy = target with { };
            Assert.Equal(target, copy); Assert.True(target == copy); Assert.False(target != copy);
            Assert.Equal(target.GetHashCode(), copy.GetHashCode()); Assert.Equal(target.ToString(), copy.ToString());
            var (scope, command, input, alternative) = target;
            Assert.Equal(("scope", "command", "input", "alternative"), (scope, command, input, alternative));
            Assert.NotEqual(target, target with { AlternativeId = "other" });
            var context = Ready(); var contextCopy = context with { };
            Assert.Equal(context, contextCopy); Assert.True(context == contextCopy); Assert.False(context != contextCopy);
            Assert.Equal(context.GetHashCode(), contextCopy.GetHashCode()); Assert.Equal(context.ToString(), contextCopy.ToString());
            var (contextScope, open, readOnly, busy, testing) = context;
            Assert.Equal(("scope", true, false, false, false), (contextScope, open, readOnly, busy, testing));
            Assert.Equal("", context.BlockedReason); Assert.NotEqual(context, context with { Testing = true });
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceAssignmentContext Ready() => new("scope", true, false, false, false);
        private static WorkspaceAssignmentTarget Target() => new("scope", "command", "input", "alternative");
        #endregion END private helpers
    }
}
