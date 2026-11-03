namespace Heimdall.Application.Games
{
    /// <summary>Defines assignment/test action availability independently of UI controls.</summary>
    /// <param name="Apply">Apply a valid changed draft.</param>
    /// <param name="Clear">Clear an owned occupied slot.</param>
    /// <param name="Start">Start exact-controller feedback.</param>
    /// <param name="Stop">Stop active feedback, including while busy.</param>
    public sealed record GameAssignmentActions(bool Apply, bool Clear, bool Start, bool Stop)
    {
        #region BEGIN Public
        /// <summary>Evaluates the agreed idle, editing, testing and busy truth table.</summary>
        public static GameAssignmentActions For(bool editable, bool valid, bool changed, bool assigned,
            bool ready, bool running, bool busy)
        {
            var context = new WorkspaceAssignmentContext("", true, !editable, busy, running);
            return new(
                WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Apply, context, new(true, valid && changed)).Enabled,
                WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Clear, context, new(true, assigned)).Enabled,
                WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StartInput, context, new(true, ready)).Enabled,
                WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StopInput, context, new(true, true)).Enabled);
        }
        #endregion END Public
    }
}
