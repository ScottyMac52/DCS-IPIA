using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Allows explicit physical association without requiring native roster mutation support.</summary>
    public interface ISimCapturedDeviceLinkValidator
    {
        #region BEGIN Public
        /// <summary>Rejects foreign or unsupported saved device references before physical association.</summary>
        void ValidateCapturedDeviceLink(ProfileDocument profile, string logicalDeviceId);
        #endregion END Public
    }
}
