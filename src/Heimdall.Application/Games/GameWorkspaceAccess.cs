namespace Heimdall.Application.Games
{
    /// <summary>Applies simulator entitlement independently of hardware names or native device tokens.</summary>
    public static class GameWorkspaceAccess
    {
        #region BEGIN Public
        /// <summary>Requires paid access for every non-DCS game workspace, including keyboard-only and empty profiles.</summary>
        public static void Require(GameWorkspace workspace, EditionPolicy policy) => policy.RequireSimulator(workspace.Profile.BackendId);
        /// <summary>Recognizes only the native keyboard/mouse tokens, never controller name aliases.</summary>
        public static bool IsSystemDevice(string? token) => token is "Keyboard" or "Mouse";
        #endregion END Public
    }
}
