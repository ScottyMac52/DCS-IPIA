namespace Heimdall.Application.Games
{
    /// <summary>A supported ownership filter and its adapter-owned label.</summary>
    public sealed record GameCommandStatusOption(string Label, GameCommandAssignmentStatus Status);
}
