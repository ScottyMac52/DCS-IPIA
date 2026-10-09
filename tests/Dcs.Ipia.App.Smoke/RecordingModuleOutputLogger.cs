using Microsoft.Extensions.Logging;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Captures UI failure events and their exception objects for regression assertions.</summary>
    internal sealed class RecordingModuleOutputLogger<T> : ILogger<T>
    {
        #region BEGIN Public
        /// <summary>Gets captured failure events.</summary>
        public List<ModuleOutputLogEntry> Events { get; } = [];
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel level) => true;
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc/>
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Events.Add(new(level, id, formatter(state, exception), exception));
        #endregion END Public
    }
}
