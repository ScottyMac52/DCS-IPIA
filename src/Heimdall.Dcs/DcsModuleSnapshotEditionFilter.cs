using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reviews edition compatibility and builds a verified supported-only copy before snapshot persistence.</summary>
    public static class DcsModuleSnapshotEditionFilter
    {
        #region BEGIN Public
        /// <summary>Lists unsupported controller profiles, including connected devices with conflicting native manufacturer evidence.</summary>
        public static IReadOnlyList<DcsDeviceProfile> Unsupported(DcsEditorWorkspaceSnapshot workspace, EditionPolicy policy,
            IReadOnlyList<DetectedDcsDevice>? observed = null) => workspace.Configuration.Profiles.Where(profile =>
                !IsKeyboard(profile) && (!policy.AllowsHardware(profile.DeviceId) || !policy.HasPremium && observed is not null &&
                    observed.Any(device => device.Connected && device.PhysicalGuid == DcsPhysicalIdentity.FromFileName(profile.SourceName) && !policy.AllowsDevice(device)))).ToArray();
        /// <summary>Reports whether any allowed profile survives removal, including the existing Free keyboard exception.</summary>
        public static bool HasSupportedProfiles(DcsEditorWorkspaceSnapshot workspace, IReadOnlyList<DcsDeviceProfile> unsupported) =>
            workspace.Configuration.Profiles.Any(p => unsupported.All(x => x.Id != p.Id));
        /// <summary>Validates the original, prunes only incompatible controllers, and verifies the resulting package before any write.</summary>
        public static DcsModuleBuildSnapshot KeepSupported(DcsModuleBuildSnapshot snapshot, EditionPolicy policy,
            IReadOnlyList<DetectedDcsDevice>? observed = null)
        {
            DcsModuleSnapshotWorkflow.Build(snapshot);
            var workspace = DcsEditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            var unsupported = Unsupported(workspace, policy, observed);
            if (unsupported.Count == 0) return snapshot;
            if (!HasSupportedProfiles(workspace, unsupported)) throw new InvalidOperationException("No supported device profiles would remain. The module snapshot was not imported.");
            var filtered = DcsWorkspaceStructureEditor.RemoveDevicesForSnapshotImport(workspace, unsupported.Select(p => p.Id).ToHashSet());
            return DcsModuleSnapshotWorkflow.Freeze(filtered, snapshot.Version);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool IsKeyboard(DcsDeviceProfile profile) => profile.SourceName.Equals("Keyboard.diff.lua", StringComparison.OrdinalIgnoreCase);
        #endregion END private helpers
    }
}
