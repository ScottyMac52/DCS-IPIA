using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reviews edition compatibility and builds a verified supported-only copy before snapshot persistence.</summary>
    public static class ModuleSnapshotEditionFilter
    {
        #region BEGIN Public
        /// <summary>Lists unsupported controller profiles, including connected devices with conflicting native manufacturer evidence.</summary>
        public static IReadOnlyList<DeviceProfile> Unsupported(EditorWorkspaceSnapshot workspace, EditionPolicy policy,
            IReadOnlyList<DetectedDcsDevice>? observed = null) => workspace.Configuration.Profiles.Where(profile =>
                !IsKeyboard(profile) && (!policy.AllowsHardware(profile.DeviceId) || !policy.IsOwner && observed is not null &&
                    observed.Any(device => device.Connected && device.PhysicalGuid == DcsPhysicalIdentity.FromFileName(profile.SourceName) && !policy.AllowsDevice(device)))).ToArray();
        /// <summary>Reports whether any allowed profile survives removal, including the existing Free keyboard exception.</summary>
        public static bool HasSupportedProfiles(EditorWorkspaceSnapshot workspace, IReadOnlyList<DeviceProfile> unsupported) =>
            workspace.Configuration.Profiles.Any(p => unsupported.All(x => x.Id != p.Id));
        /// <summary>Validates the original, prunes only incompatible controllers, and verifies the resulting package before any write.</summary>
        public static ModuleBuildSnapshot KeepSupported(ModuleBuildSnapshot snapshot, EditionPolicy policy,
            IReadOnlyList<DetectedDcsDevice>? observed = null)
        {
            ModuleSnapshotWorkflow.Build(snapshot);
            var workspace = EditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            var unsupported = Unsupported(workspace, policy, observed);
            if (unsupported.Count == 0) return snapshot;
            if (!HasSupportedProfiles(workspace, unsupported)) throw new InvalidOperationException("No supported device profiles would remain. The module snapshot was not imported.");
            var filtered = WorkspaceStructureEditor.RemoveDevicesForSnapshotImport(workspace, unsupported.Select(p => p.Id).ToHashSet());
            return ModuleSnapshotWorkflow.Freeze(filtered, snapshot.Version);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool IsKeyboard(DeviceProfile profile) => profile.SourceName.Equals("Keyboard.diff.lua", StringComparison.OrdinalIgnoreCase);
        #endregion END private helpers
    }
}
