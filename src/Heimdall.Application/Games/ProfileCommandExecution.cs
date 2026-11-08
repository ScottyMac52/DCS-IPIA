namespace Heimdall.Application.Games
{
    /// <summary>Pairs the native command result with the shared completion, including durable-write warnings and recovery.</summary>
    /// <param name="Native">The native receipt, including any saved identity or output path.</param>
    /// <param name="Completion">The shared operation outcome; a committed result must not be retried as a failed write.</param>
    public sealed record ProfileCommandExecution<TResult>(TResult Native, WorkspaceOperationResult Completion);
}
