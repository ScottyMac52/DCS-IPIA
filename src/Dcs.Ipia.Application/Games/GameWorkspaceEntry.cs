namespace Dcs.Ipia.Application.Games
{
    /// <summary>A saved profile choice without native configuration contents.</summary>
    /// <param name="Id">Workspace identity.</param>
    /// <param name="Name">User-facing profile name.</param>
    /// <param name="BackendId">Owning game.</param>
    public sealed record GameWorkspaceEntry(Guid Id, string Name, string BackendId);
}
