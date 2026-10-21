namespace Heimdall.TestSupport
{
    /// <summary>Supplies a deterministic monotonic clock for identification settling tests.</summary>
    public sealed class IdentificationTestTime : TimeProvider
    {
        private long _ticks;
        #region BEGIN Public
        /// <summary>Gets timestamp ticks per second.</summary>
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <summary>Returns the controlled monotonic timestamp.</summary>
        public override long GetTimestamp() => _ticks;
        /// <summary>Advances the clock without sleeping or running dispatcher timers.</summary>
        public void Advance(int milliseconds = 600) => _ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
        #endregion END Public
    }
}
