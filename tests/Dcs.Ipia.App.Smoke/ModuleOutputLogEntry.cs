using Microsoft.Extensions.Logging;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Retains the complete failure event emitted by an actual UI handler.</summary>
    internal sealed record ModuleOutputLogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}
