using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>One displayed input-test event with separate columns for clipboard export.</summary>
    /// <param name="Time">Local time when the transition was sampled.</param>
    /// <param name="PhysicalGuid">Exact controller identity, or the foreground keyboard.</param>
    /// <param name="InputKey">Physical input name.</param>
    /// <param name="Transition">Press, Release, or Position.</param>
    /// <param name="Value">Normalized input value.</param>
    /// <param name="ActiveModifiers">Modifier state at the time of the event.</param>
    /// <param name="Commands">Resolved generated commands and tuning details.</param>
    public sealed record InputTestLogEntry(DateTime Time, Guid PhysicalGuid, string InputKey, string Transition,
        double Value, string ActiveModifiers, string Commands)
    {
        #region BEGIN Public
        /// <summary>Gets the controller name displayed in the event log.</summary>
        public string Device => PhysicalGuid == Guid.Empty ? "Keyboard" : PhysicalGuid.ToString("D");
        /// <summary>Gets the original readable event-log representation.</summary>
        public string Display => $"{Time:HH:mm:ss.fff} {Device} {InputKey} {Transition} {Value.ToString("F3", CultureInfo.InvariantCulture)} · active {ActiveModifiers} → {Commands}";
        /// <summary>Returns the readable event representation for diagnostics.</summary>
        /// <returns>The displayed event text.</returns>
        public override string ToString() => Display;
        #endregion END Public
    }
}
