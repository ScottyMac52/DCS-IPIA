using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>A decoded joystick input; axis direction is independent of the native record's negate flag.</summary>
    /// <param name="Key">Shared physical input key.</param>
    /// <param name="Type">Axis, button or hat.</param>
    /// <param name="Direction">Positive/negative axis direction, or zero for buttons and hats.</param>
    public sealed record SquadronsInput(string Key, CommandType Type, int Direction)
    {
        #region BEGIN Public
        /// <summary>Gets a readable physical input label for selection and assignment tables.</summary>
        public string DisplayName => Type switch
        {
            CommandType.Axis => Key.Replace("JOY_", "", StringComparison.Ordinal) + " axis (" + (Direction > 0 ? "positive" : "negative") + ")",
            CommandType.Button => Key.Replace("JOY_BTN", "Button ", StringComparison.Ordinal),
            _ => Key.Replace("JOY_POV", "Hat ", StringComparison.Ordinal).Replace("_", " · ", StringComparison.Ordinal)
        };
        #endregion END Public
    }
}
