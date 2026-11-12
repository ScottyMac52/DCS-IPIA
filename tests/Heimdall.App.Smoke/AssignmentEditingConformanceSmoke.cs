using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises one shared editing policy through the four composed production roles.</summary>
    internal static class AssignmentEditingConformanceSmoke
    {
        #region BEGIN Public
        internal static void Verify(GameAssignmentsView view, IGameWorkspaceContext context)
        {
            var role = view.Editing;
            var baseline = context.CaptureSnapshot();
            var dirty = context.State.Dirty;
            var workflowCompletions = 0;
            var roleCompletions = 0;
            EventHandler<DraftResult> workflowCompleted = (_, _) => workflowCompletions++;
            EventHandler<DraftResult> roleCompleted = (_, _) => roleCompletions++;
            context.AssignmentWorkflow.Completed += workflowCompleted;
            role.Completed += roleCompleted;
            try
            {
                DraftResult Run(Func<GameAssignmentReview> review, Action apply) =>
                    view.ExecuteNativeAssignment("contract-command", "contract-input", "contract-record", review, apply);

                var applied = 0;
                var noChange = Run(() => new(true, true, ""), () => applied++);
                Require(noChange.Outcome == DraftOutcome.NoChange && applied == 1,
                    "An admitted native no-op was not evaluated exactly once.");
                Require(context.CaptureSnapshot() == baseline && context.State.Dirty == dirty, "A no-op changed the draft.");

                var canceled = Run(() => new(false, false, "Canceled for editing conformance."), () => applied++);
                Require(canceled.Outcome == DraftOutcome.Blocked && applied == 1, "A canceled review reached native mutation.");

                var originalName = context.State.EditedName;
                try
                {
                    var stale = Run(() => { context.SetEditedName(originalName + " review"); return new(true, true, ""); }, () => applied++);
                    Require(stale.Outcome == DraftOutcome.Blocked && applied == 1 &&
                        context.State.EditedName != originalName, "A stale review overwrote pending state.");
                    var dirtyNoOp = Run(() => new(true, false, ""), () => applied++);
                    Require(dirtyNoOp.Outcome == DraftOutcome.NoChange && context.State.Dirty && applied == 1,
                        "A no-op on a dirty draft applied or lost pending state.");
                }
                finally { context.SetEditedName(originalName); }

                var reentrant = Run(() =>
                {
                    var nested = Run(() => new(true, true, ""), () => applied++);
                    Require(nested.Outcome == DraftOutcome.Blocked, "A nested review was admitted.");
                    return new(true, false, "");
                }, () => applied++);
                Require(reentrant.Outcome == DraftOutcome.NoChange && applied == 1, "Reentry mutated native state.");

                try
                {
                    Run(() => throw new InvalidOperationException("review probe"), () => applied++);
                    throw new InvalidOperationException("The native review exception was swallowed.");
                }
                catch (InvalidOperationException error) when (error.Message == "review probe") { }
                Require(workflowCompletions == 5 && roleCompletions == 5 && applied == 1,
                    "A canceled, no-op, stale, failed or reentrant edit emitted an incorrect completion count.");
                Require(context.CaptureSnapshot() == baseline && context.State.Dirty == dirty,
                    "Editing conformance changed the workspace draft.");
            }
            finally
            {
                context.AssignmentWorkflow.Completed -= workflowCompleted;
                role.Completed -= roleCompleted;
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        #endregion END private helpers
    }
}
