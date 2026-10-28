using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>Intersecting command filters. Null section/category/type means all available values.</summary>
    public sealed record GameCommandQuery(string? Section = null, string? Category = null,
        CommandType? InputType = null, string Search = "",
        GameCommandAssignmentStatus AssignmentStatus = GameCommandAssignmentStatus.All);
}
