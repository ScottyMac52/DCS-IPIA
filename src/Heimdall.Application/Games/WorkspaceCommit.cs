namespace Heimdall.Application.Games
{
    /// <summary>A durable native save: the persisted candidate and its native receipt (store revision or DCS saved snapshot).</summary>
    public sealed record WorkspaceCommit<TSnapshot, TCommit>(TSnapshot Snapshot, TCommit Receipt) where TSnapshot : class;
}
