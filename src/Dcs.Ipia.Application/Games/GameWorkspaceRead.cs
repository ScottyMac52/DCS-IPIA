namespace Dcs.Ipia.Application.Games
{
    /// <summary>Saved state plus an optimistic concurrency revision.</summary>
    /// <param name="Workspace">Independent saved workspace.</param>
    /// <param name="Revision">Hash of the exact persisted document.</param>
    public sealed record GameWorkspaceRead(GameWorkspace Workspace, string Revision);
}
