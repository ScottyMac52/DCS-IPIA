using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Adds reviewed physical units without using catalog filenames as discovery evidence.</summary>
    public static class NewModuleDeviceAuthoring
    {
        #region BEGIN Public
        /// <summary>Applies one physical choice, preserving a selected baseline profile when rebinding it.</summary>
        /// <param name="workspace">New or staged module.</param>
        /// <param name="selection">Actual native filename, hardware and optional inherited profile.</param>
        /// <returns>An independent staged module with aircraft membership.</returns>
        public static EditorWorkspaceSnapshot Apply(EditorWorkspaceSnapshot workspace, NewModuleDeviceSelection selection)
        {
            if (DcsPhysicalIdentity.FromFileName(selection.FileName) is null)
                throw new InvalidOperationException("Select a physical unit or enter its GUID-qualified DCS filename. A template filename is not a physical identity.");
            if (selection.BaselineFileName is not null)
            {
                var profile = workspace.Configuration.Profiles.SingleOrDefault(x =>
                    x.SourceName.Equals(selection.BaselineFileName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("The selected copied baseline no longer exists.");
                if (profile.DeviceId != selection.Hardware.DeviceId)
                    throw new InvalidOperationException("Choose the same shared hardware as the copied baseline.");
                var device = WorkspaceStructureEditor.Describe(workspace).Devices.First(x => x.ProfileId == profile.Id);
                workspace = WorkspaceStructureEditor.RebindDevice(workspace, device, selection.FileName, selection.Instance, selection.Role);
            }
            return WorkspaceStructureEditor.AddDevice(workspace, selection.Hardware, AssignmentLayer.Module,
                selection.FileName, selection.Instance, selection.Role);
        }
        #endregion END Public
    }
}
