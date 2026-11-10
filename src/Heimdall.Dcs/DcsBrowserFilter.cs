using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Dcs
{
    /// <summary>Native DCS layer/controller and every-device assignment-status policy.</summary>
    public sealed record DcsBrowserFilter(AssignmentLayer Layer = AssignmentLayer.Module, Guid? ControllerId = null,
        string? Category = null, CommandType? InputType = null, DcsCommandAssignmentStatus Status = DcsCommandAssignmentStatus.All);
}
