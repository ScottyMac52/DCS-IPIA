namespace Heimdall.Application.Games
{
    /// <summary>Coordinates guarded native workspace transitions independently of UI or persistence implementation.</summary>
    public static class GameWorkspaceLeaveGuard
    {
        #region BEGIN Public
        /// <summary>Prompts only for pending changes; a failed save or cancellation preserves the draft.</summary>
        public static bool Confirm(bool pending, Func<GameWorkspaceLeaveDecision> choose,
            Func<bool> save, Action discard)
        {
            ArgumentNullException.ThrowIfNull(choose);
            ArgumentNullException.ThrowIfNull(save);
            ArgumentNullException.ThrowIfNull(discard);
            if (!pending) return true;
            return choose() switch
            {
                GameWorkspaceLeaveDecision.Save => save(),
                GameWorkspaceLeaveDecision.Discard => Discard(),
                _ => false
            };
            bool Discard() { discard(); return true; }
        }
        /// <summary>Waits for native asynchronous persistence before allowing the workspace to leave.
        /// Cancel, an unsuccessful save, or an exception never invokes discard.</summary>
        public static async Task<bool> ConfirmAsync(bool pending, Func<GameWorkspaceLeaveDecision> choose,
            Func<Task<bool>> save, Action discard)
        {
            ArgumentNullException.ThrowIfNull(choose);
            ArgumentNullException.ThrowIfNull(save);
            ArgumentNullException.ThrowIfNull(discard);
            if (!pending) return true;
            switch (choose())
            {
                case GameWorkspaceLeaveDecision.Save: return await save().ConfigureAwait(false);
                case GameWorkspaceLeaveDecision.Discard: discard(); return true;
                default: return false;
            }
        }
        /// <summary>Recovery-aware leave: a stale session whose save already committed (<see cref="WorkspaceState.Recovery"/>)
        /// holds no unsaved work, so it is abandoned without a prompt, a stale discard or another write; the next open reads the
        /// durable profile. Every other workspace keeps the ordinary pending-change guard.</summary>
        public static Task<bool> ConfirmAsync(WorkspaceState? state, bool pending, Func<GameWorkspaceLeaveDecision> choose,
            Func<Task<bool>> save, Action discard) =>
            state is { Recovery.Length: > 0 } ? Task.FromResult(true) : ConfirmAsync(pending, choose, save, discard);
        #endregion END Public
    }
}
