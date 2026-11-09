using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Native ED section, controller and command filters; search is owned by the shared browser.</summary>
    public sealed record EliteBrowserFilter(string Section = "All", string? ControllerId = null, string? Category = null,
        CommandType? InputType = null, GameCommandAssignmentStatus Status = GameCommandAssignmentStatus.All);
}
