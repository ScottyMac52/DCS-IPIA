using System.Text.Json;

namespace Dcs.Ipia.Application
{
    /// <summary>Persists editable device membership and modifiers in the workspace's source envelope.</summary>
    /// <param name="Devices">Scope-specific device membership.</param>
    /// <param name="Modifiers">Scope-specific modifier definitions.</param>
    public sealed record WorkspaceManagement(IReadOnlyList<WorkspaceDevice> Devices,
                                              IReadOnlyList<WorkspaceModifier> Modifiers)
    {
        #region BEGIN Public
        /// <summary>Gets the module-owned settings source path.</summary>
        public const string SourcePath = "config/ipia-management.json";
        /// <summary>Reads explicit settings, returning null for legacy imported workspaces.</summary>
        /// <param name="workspace">Stored or staged workspace.</param>
        /// <returns>Explicit settings when management has been used.</returns>
        public static WorkspaceManagement? Read(EditorWorkspaceSnapshot workspace) =>
            workspace.Sources?.TryGetValue(SourcePath, out var source) == true
                ? JsonSerializer.Deserialize<WorkspaceManagement>(source) ??
                  throw new InvalidDataException("Device and modifier settings are empty.")
                : null;
        #endregion END Public
    }
}
