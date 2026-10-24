using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>A decoded joystick input; axis direction is independent of the native record's negate flag.</summary>
    /// <param name="Key">Shared physical input key.</param>
    /// <param name="Type">Axis, button or hat.</param>
    /// <param name="Direction">Positive/negative axis direction, or zero for buttons and hats.</param>
    public sealed record SquadronsInput(string Key, CommandType Type, int Direction);
}
