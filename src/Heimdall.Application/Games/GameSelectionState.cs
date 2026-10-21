namespace Heimdall.Application.Games
{
    /// <summary>Last explicitly selected game/profile; retains an optional ED export directory override.</summary>
    /// <param name="BackendId">Supported game identifier.</param>
    /// <param name="WorkspaceId">Optional last ED workspace.</param>
    /// <param name="DcsModuleId">Optional last DCS module.</param>
    /// <param name="DisplayName">Recent profile label.</param>
    public sealed record GameSelectionState(string BackendId = "dcs", Guid? WorkspaceId = null,
        Guid? DcsModuleId = null, string DisplayName = "")
    {
        #region BEGIN Public
        /// <summary>Gets an optional absolute export directory; null uses the current user's standard ED folder.</summary>
        public string? EliteBindingsDirectory { get; init; }
        /// <summary>Gets the remembered controller identification settling period in seconds.</summary>
        public double EliteIdentificationSettlingSeconds { get; init; } = .5;
        #endregion END Public
    }
}
