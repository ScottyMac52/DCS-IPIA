
namespace Heimdall.Application.Games
{
    /// <summary>Native eligibility for shared actions, intersected with current context gates.</summary>
    public sealed record WorkspaceCapabilities(WorkspaceAvailability Save, WorkspaceAvailability Discard, WorkspaceAvailability Edit, WorkspaceAvailability Scan, WorkspaceAvailability Identify, WorkspaceAvailability Test, WorkspaceAvailability Link);
}
