
namespace Heimdall.Application.Games
{
    /// <summary>The immutable state shared by window, feature, editor and input entry points.</summary>
    public sealed record WorkspaceState(WorkspaceIdentity Identity, long Version, bool Open, bool ReadOnly, bool Dirty, string EditedName, WorkspaceSelection Selection, WorkspaceOperationKind? Operation, WorkspaceActivityKind? Activity, WorkspaceCapabilities Capabilities);
}
