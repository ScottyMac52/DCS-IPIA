namespace Dcs.Ipia.Application.Games
{
    /// <summary>Last explicitly selected game/profile; contains no active game path.</summary>
    /// <param name="BackendId">Supported game identifier.</param>
    /// <param name="WorkspaceId">Optional last ED workspace.</param>
    /// <param name="DcsModuleId">Optional last DCS module.</param>
    /// <param name="DisplayName">Recent profile label.</param>
    public sealed record GameSelectionState(string BackendId = "dcs", Guid? WorkspaceId = null,
        Guid? DcsModuleId = null, string DisplayName = "");
}
