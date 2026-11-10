using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using Heimdall.Application;

namespace Heimdall.App
{
    /// <summary>Samples dispatcher responsiveness and writes one local capture on a clean Profile-build exit.</summary>
    internal sealed class PerformanceSession : IDisposable
    {
        private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
        private readonly DispatcherTimer _timer;
        private readonly string _output;
        private readonly int[] _collections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        private long _tick = Stopwatch.GetTimestamp();
        private bool _disposed;

        internal PerformanceSession(Dispatcher dispatcher)
        {
            _output = Environment.GetEnvironmentVariable("HEIMDALL_PERFORMANCE_DIRECTORY")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HEIMDALL", "Performance");
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, Tick, dispatcher);
            _timer.Start();
        }

        #region BEGIN Public
        /// <summary>Stops sampling and writes bounded telemetry without letting report IO failures prevent shutdown.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _timer.Stop(); _timer.Tick -= Tick;
            try
            {
                using var process = Process.GetCurrentProcess();
                var report = new
                {
                    SchemaVersion = 1, Configuration = "Profile", StartedUtc = _started, EndedUtc = DateTimeOffset.UtcNow,
                    Build = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
                    Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(), LogicalProcessors = Environment.ProcessorCount,
                    ProcessCpuMs = process.TotalProcessorTime.TotalMilliseconds, PrivateBytesAtExit = process.PrivateMemorySize64,
                    ManagedAllocatedBytes = GC.GetTotalAllocatedBytes(),
                    Collections = Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - _collections[generation]).ToArray(),
                    Capacity = 100_000, Dropped = PerformanceTrace.Dropped, Samples = PerformanceTrace.Snapshot()
                };
                Directory.CreateDirectory(_output);
                var file = Path.Combine(_output, $"capture-{_started:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.json");
                File.WriteAllText(file, JsonSerializer.Serialize(report));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                Trace.TraceWarning("Performance capture could not be written: {0}", error.Message);
            }
        }
        #endregion END Public

        #region BEGIN private helpers
        private void Tick(object? sender, EventArgs args)
        {
            var now = Stopwatch.GetTimestamp();
            PerformanceTrace.Record("dispatcher.heartbeat-delay", Math.Max(0, Stopwatch.GetElapsedTime(_tick, now).TotalMilliseconds - 100));
            _tick = now;
        }
        #endregion END private helpers
    }
}
