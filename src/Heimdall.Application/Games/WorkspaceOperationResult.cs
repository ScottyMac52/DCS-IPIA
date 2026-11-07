
namespace Heimdall.Application.Games
{
    /// <summary>An operation outcome with a separate durable-commit indication.</summary>
    public sealed record WorkspaceOperationResult(WorkspaceOperationOutcome Outcome, string Message, bool Committed = false);
}
