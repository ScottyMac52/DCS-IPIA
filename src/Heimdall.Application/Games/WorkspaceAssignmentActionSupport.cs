namespace Heimdall.Application.Games
{
    /// <summary>Adapter evidence for an operation, separate from transient workspace gates.</summary>
    /// <param name="Supported">Whether the adapter implements the operation for this input family.</param>
    /// <param name="TargetReady">Whether the current native target satisfies that operation's requirements.</param>
    /// <param name="Reason">Native target explanation when unavailable, such as an unbound axis or unsupported record shape.</param>
    public readonly record struct WorkspaceAssignmentActionSupport(bool Supported, bool TargetReady, string Reason = "");
}
