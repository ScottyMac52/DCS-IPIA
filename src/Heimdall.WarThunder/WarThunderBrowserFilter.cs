using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Native War Thunder command metadata and captured-device filter.</summary>
    public sealed record WarThunderBrowserFilter(string? ControllerId = null, string? Category = null,
        CommandType? InputType = null, GameCommandAssignmentStatus Status = GameCommandAssignmentStatus.All);
}
