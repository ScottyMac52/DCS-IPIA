namespace Heimdall.Application
{
    /// <summary>WPF-independent wheel behavior shared by all diagram surfaces.
    /// Values are viewport offsets in device-independent pixels.</summary>
    public enum DiagramWheelMode { Vertical, Horizontal, Zoom }

    /// <summary>Pure gesture decisions and calculations so multiple simulators cannot
    /// silently diverge in scroll direction, modifiers or zoom anchoring.</summary>
    public static class DiagramWheelNavigation
    {
        public static DiagramWheelMode Mode(bool control, bool shift) =>
            control ? DiagramWheelMode.Zoom : shift ? DiagramWheelMode.Horizontal : DiagramWheelMode.Vertical;

        /// <summary>Standard wheel motion: positive delta moves toward the top/left.</summary>
        public static double ScrollDelta(int wheelDelta) => wheelDelta / 120.0 * 48;

        /// <summary>Exponential scaling gives symmetric wheel in/out factors.</summary>
        public static double Zoom(double current, int wheelDelta, double minimum, double maximum)
        {
            if (!double.IsFinite(current) || !double.IsFinite(minimum) || !double.IsFinite(maximum) ||
                current <= 0 || minimum <= 0 || maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(current), "Invalid diagram zoom bounds.");
            return Math.Clamp(current * Math.Pow(1.1, wheelDelta / 120.0), minimum, maximum);
        }

        /// <summary>Compensates the scroll offset to keep a cursor-relative image point
        /// fixed while scaling around the image origin.</summary>
        public static double AnchoredOffset(double oldOffset, double cursorOffset, double ratio) =>
            Math.Max(0, (oldOffset + cursorOffset) * ratio - cursorOffset);
    }
}
