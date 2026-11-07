namespace Heimdall.Tests
{
    /// <summary>An immutable non-game model proving the context base does not require simulator data.</summary>
    public sealed record WorkspaceTestSnapshot(string Key, string Name, string Data);
}
