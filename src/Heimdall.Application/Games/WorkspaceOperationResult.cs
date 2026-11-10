
namespace Heimdall.Application.Games
{
    /// <summary>An operation outcome with a separate durable-commit indication.</summary>
    /// <param name="Outcome">The operation outcome.</param>
    /// <param name="Message">The user-facing message.</param>
    /// <param name="Committed">Whether a durable write completed.</param>
    /// <param name="RecoveryRequired">Whether the durable write completed but the native baseline could not be installed; reopen before editing further.</param>
    public sealed record WorkspaceOperationResult(WorkspaceOperationOutcome Outcome, string Message, bool Committed = false, bool RecoveryRequired = false);
}
