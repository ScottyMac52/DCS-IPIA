using Heimdall.Domain;

namespace Heimdall.Application
{
    /// <summary>A physical device's editable membership in one module-owned scope.</summary>
    /// <param name="ProfileId">Shared physical profile identity.</param>
    /// <param name="Layer">Module or module UI Layer.</param>
    /// <param name="FileName">Editable DCS diff.lua filename.</param>
    /// <param name="Instance">Optional physical instance.</param>
    /// <param name="Role">Optional semantic role.</param>
    /// <param name="PhysicalGuid">Optional native DirectInput GUID, separate from ProfileId.</param>
    public sealed record WorkspaceDevice(Guid ProfileId, AssignmentLayer Layer, string FileName,
                                         string? Instance, string? Role, Guid? PhysicalGuid = null)
    {
        #region BEGIN Public
        /// <summary>Gets a readable device label without duplicating the full native GUID.</summary>
        public string Title => DcsDeviceDisplay.Label(FileName, Instance ?? Role ?? PhysicalGuid?.ToString("D"));
        #endregion END Public
    }
}
