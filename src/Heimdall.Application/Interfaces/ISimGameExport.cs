using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Prepares named game presets and exposes all native device usages for connected-hardware preflight.</summary>
    public interface ISimGameExport
    {
        #region BEGIN Public
        /// <summary>Reads every device reference from the validated output, including opaque native extensions.</summary>
        IReadOnlyList<SimDeviceUsage> DeviceUsages(ProfileDocument profile);
        /// <summary>Produces a named native preset with consistent filename and selector references.</summary>
        SimProfileExportPlan ExportNamed(ProfileDocument profile, string presetName);
        #endregion END Public
    }
}
