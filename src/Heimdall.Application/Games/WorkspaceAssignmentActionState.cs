namespace Heimdall.Application.Games
{
    /// <summary>One capability-driven availability policy for toolbars, editors, callouts and context menus.</summary>
    /// <param name="Supported">Unsupported operations are hidden by common presentation.</param>
    /// <param name="Enabled">Supported operations require fresh workspace gates and a native target.</param>
    /// <param name="Reason">Shared gate or native target explanation shown beside unavailable actions.</param>
    public sealed record WorkspaceAssignmentActionState(bool Supported, bool Enabled, string Reason)
    {
        #region BEGIN Public
        /// <summary>Combines adapter evidence with live workspace state; no simulator or UI types enter this policy.</summary>
        public static WorkspaceAssignmentActionState For(WorkspaceAssignmentAction action,
            WorkspaceAssignmentContext context, WorkspaceAssignmentActionSupport support)
        {
            if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (!support.Supported) return new(false, false, "This operation is not supported by the adapter.");
            var blocked = Gate(action, context);
            if (blocked.Length != 0) return new(true, false, blocked);
            return support.TargetReady ? new(true, true, "") : new(true, false,
                string.IsNullOrEmpty(support.Reason) ? "Select a compatible target for this action." : support.Reason);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Gate(WorkspaceAssignmentAction action, WorkspaceAssignmentContext context)
        {
            if (action == WorkspaceAssignmentAction.StopInput)
                return context.Testing ? "" : "Input testing is not running.";
            if (!context.Open) return context.BlockedReason;
            if (action is WorkspaceAssignmentAction.Inspect or WorkspaceAssignmentAction.Fit) return "";
            if (action == WorkspaceAssignmentAction.StartInput)
                return context.Testing ? "Input testing is already running."
                    : context.Busy ? "Finish the current operation before starting input testing." : "";
            return context.BlockedReason;
        }
        #endregion END private helpers
    }
}
