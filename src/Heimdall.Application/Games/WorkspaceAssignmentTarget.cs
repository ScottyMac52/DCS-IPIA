namespace Heimdall.Application.Games
{
    /// <summary>An immutable action target bound to its workspace/controller scope, command, input and native alternative.</summary>
    public sealed record WorkspaceAssignmentTarget(string Scope, string CommandId, string InputId, string AlternativeId);
}
