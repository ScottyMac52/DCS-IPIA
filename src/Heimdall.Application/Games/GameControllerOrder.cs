namespace Heimdall.Application.Games
{
    /// <summary>Reconciles saved presentation preferences with current native references without editing them.</summary>
    public static class GameControllerOrder
    {
        #region BEGIN Public
        /// <summary>Retains ordered surviving references, appending newly added devices in native order.</summary>
        public static IReadOnlyList<string> For(GameWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            var ids = workspace.Profile.Devices.Select(x => x.LogicalId).ToArray();
            return workspace.ControllerOrder.Where(id => ids.Contains(id, StringComparer.Ordinal))
                .Concat(ids).Distinct(StringComparer.Ordinal).ToArray();
        }
        #endregion END Public
    }
}
