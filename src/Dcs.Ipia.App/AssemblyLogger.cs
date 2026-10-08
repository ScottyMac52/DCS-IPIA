using Microsoft.Extensions.Logging;

namespace DcsIpia.Presentation
{
    /// <summary>Makes JSON overrides follow the owning assembly even when namespaces differ.</summary>
    /// <typeparam name="T">The type emitting the event.</typeparam>
    internal sealed class AssemblyLogger<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        /// <summary>Creates a category prefixed with the actual assembly name.</summary>
        /// <param name="factory">The DI-owned Serilog logger factory.</param>
        public AssemblyLogger(ILoggerFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _logger = factory.CreateLogger(typeof(T).Assembly.GetName().Name + "." + typeof(T).FullName);
        }

        #region BEGIN Public
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => _logger.Log(logLevel, eventId, state, exception, formatter);
        #endregion END Public
    }
}
