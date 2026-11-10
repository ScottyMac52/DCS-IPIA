
namespace Heimdall.Application.Games
{
    /// <summary>The immutable state shared by window, feature, editor and input entry points.</summary>
    public sealed record WorkspaceState(WorkspaceIdentity Identity, long Version, bool Open, bool ReadOnly, bool Dirty, string EditedName, WorkspaceSelection Selection, WorkspaceOperationKind? Operation, WorkspaceActivityKind? Activity, WorkspaceCapabilities Capabilities)
    {
        #region BEGIN Public
        /// <summary>Gets why the mounted native session is stale after a committed save it could not install, or empty.
        /// While set, the session may only be abandoned or replaced by a fresh one; it can never be edited, saved or discarded.</summary>
        public string Recovery { get; init; } = "";
        #endregion END Public
    }
}
