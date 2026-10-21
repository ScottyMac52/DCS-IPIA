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
            bool ready, bool running, bool busy) => new(
                editable && valid && changed && !running && !busy,
                editable && assigned && !running && !busy,
                ready && !running && !busy, running);
        #endregion END Public
    }
}
