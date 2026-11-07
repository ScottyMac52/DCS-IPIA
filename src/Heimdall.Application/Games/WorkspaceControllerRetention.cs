namespace Heimdall.Application.Games
{
    /// <summary>Chooses which controller stays selected when a native view is rebuilt.</summary>
    public static class WorkspaceControllerRetention
    {
        #region BEGIN Public
        /// <summary>Prefers the context selection, then the controller still displayed in the picker (session replacement on save
        /// resets context selection), then the first controller.</summary>
        public static string? Choose(IReadOnlyList<string> controllerIds, string? selected, string? displayed)
        {
            ArgumentNullException.ThrowIfNull(controllerIds);
            if (selected is not null && controllerIds.Contains(selected, StringComparer.Ordinal)) return selected;
            if (displayed is not null && controllerIds.Contains(displayed, StringComparer.Ordinal)) return displayed;
            return controllerIds.Count > 0 ? controllerIds[0] : null;
        }
        #endregion END Public
    }
}
