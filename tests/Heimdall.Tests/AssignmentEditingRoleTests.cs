using Heimdall.Application;
using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises editing through the context-owned workflow, including review that changes native state.</summary>
    public sealed class AssignmentEditingRoleTests
    {
        #region BEGIN Public
        /// <summary>Rechecks the native capability after review and publishes exactly one result.</summary>
        [Fact]
        public void Capability_lost_during_review_never_applies()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            using var editing = new AssignmentEditingProbe(context);
            var completed = 0;
            context.AssignmentWorkflow.Completed += (_, _) => completed++;
            var forwarded = 0;
            editing.Completed += (_, _) => forwarded++;
            var operation = new WorkspaceAssignmentOperation(new(context.Context.Scope, "command", "input", "slot"),
                () => { editing.Ready = false; return new(true, true, ""); },
                () => throw new InvalidOperationException("Stale operation reached Apply."));

            Assert.Equal(DraftOutcome.Blocked, editing.Execute(WorkspaceAssignmentAction.Apply, operation).Outcome);
            Assert.Equal(1, completed);
            Assert.Equal(1, forwarded);
            Assert.Equal("saved", context.Working!.Data);
            Assert.Null(context.AssignmentWorkflow.Selection);
        }
        /// <summary>A completion subscriber cannot reenter and a failed review releases the original workflow.</summary>
        [Fact]
        public void Completion_reentry_and_exception_do_not_duplicate_native_writes()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            using var editing = new AssignmentEditingProbe(context);
            var target = new WorkspaceAssignmentTarget(context.Context.Scope, "command", "input", "slot");
            var writes = 0;
            var completions = 0;
            var operation = new WorkspaceAssignmentOperation(target, () => new(true, true, ""),
                () => { context.Edit("staged"); writes++; return new(DraftOutcome.Applied, "Native success."); });
            editing.Completed += (_, _) =>
            {
                completions++;
                Assert.Equal(DraftOutcome.Blocked, editing.Execute(WorkspaceAssignmentAction.Apply, operation).Outcome);
            };
            Assert.Throws<InvalidOperationException>(() => editing.Execute(WorkspaceAssignmentAction.Apply,
                new WorkspaceAssignmentOperation(target, () => throw new InvalidOperationException("Review failed."),
                    () => throw new InvalidOperationException("Review failure reached Apply."))));
            Assert.Null(context.AssignmentWorkflow.Selection);
            Assert.Equal(0, completions);
            Assert.Equal(0, writes);

            Assert.Equal(DraftOutcome.Applied, editing.Execute(WorkspaceAssignmentAction.Apply, operation).Outcome);
            Assert.Equal("Native success.", editing.Feedback?.Message);
            Assert.Equal(1, completions);
            Assert.Equal(1, writes);
            Assert.Equal(2, editing.Refreshes);
        }

        /// <summary>Unsupported native actions are explained and a target changed during review is stale.</summary>
        [Fact]
        public void Action_admission_observes_support_and_selected_input()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            using var editing = new AssignmentEditingProbe(context) { Supported = false };
            Assert.False(editing.ActionState(WorkspaceAssignmentAction.TuneAxis).Supported);
            context.Select(context.State.Selection with { CommandId = "command" });
            var target = new WorkspaceAssignmentTarget(context.Context.Scope, "command", "input", "slot");
            var operation = new WorkspaceAssignmentOperation(target,
                () => { context.Select(context.State.Selection with { InputId = "changed" }); return new(true, true, ""); },
                () => throw new InvalidOperationException("Unsupported or stale action reached Apply."));
            var unsupported = editing.Execute(WorkspaceAssignmentAction.TuneAxis, operation);
            Assert.Contains("not supported", unsupported.Message, StringComparison.Ordinal);
            editing.Supported = true;
            Assert.True(editing.ActionState(WorkspaceAssignmentAction.TuneAxis, new(true, true)).Enabled);
            var stale = editing.Execute(WorkspaceAssignmentAction.TuneAxis, operation);
            Assert.Equal(DraftOutcome.Blocked, stale.Outcome);
            Assert.Contains("selection changed", stale.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("saved", context.Working!.Data);
        }
        #endregion END Public
    }
}
