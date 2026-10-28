using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>Adapter-supplied display facts. Native records and format-specific metadata stay in the adapter.</summary>
    public sealed record GameCommandEntry(SimCommand Command, string Section, string Category,
        IReadOnlyCollection<CommandType> InputTypes, IReadOnlyCollection<string> AssignedControllers);

    /// <summary>Display-only assignment ownership; combination inputs do not own a command.</summary>
    public enum GameCommandAssignmentStatus
    {
        All, AssignedToController, NotAssignedToController, UnassignedEverywhere, AssignedAnywhere
    }

    /// <summary>Intersecting command filters. Null section/category/type means all available values.</summary>
    public sealed record GameCommandQuery(string? Section = null, string? Category = null,
        CommandType? InputType = null, string Search = "",
        GameCommandAssignmentStatus AssignmentStatus = GameCommandAssignmentStatus.All);

    /// <summary>A supported input filter and its adapter-owned label.</summary>
    public sealed record GameCommandTypeOption(string Label, CommandType? Type);

    /// <summary>A supported ownership filter and its adapter-owned label.</summary>
    public sealed record GameCommandStatusOption(string Label, GameCommandAssignmentStatus Status);
}
