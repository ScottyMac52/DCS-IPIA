using System.Diagnostics;

namespace Heimdall.Application
{
    /// <summary>Measures one synchronous or asynchronous operation, including time spent in nested work.</summary>
    public readonly struct PerformanceScope : IDisposable
    {
        private readonly PerformanceCapture? _capture;
        private readonly string? _operation, _component;
        private readonly long _started;

        internal PerformanceScope(PerformanceCapture capture, string operation, string component)
        {
            _capture = capture; _operation = operation; _component = component;
            _started = Stopwatch.GetTimestamp();
        }

        #region BEGIN Public
        /// <summary>Records elapsed time when a scope exits, including exits caused by exceptions.</summary>
        public void Dispose() => _capture?.Record(_operation!, _component!, Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
        #endregion END Public
    }
}
