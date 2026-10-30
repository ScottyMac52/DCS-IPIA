using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Opts an adapter into reviewed changes to native controller declaration names.</summary>
    public interface INativeControllerNames
    {
        #region BEGIN Public
        /// <summary>Changes one native controller name while retaining its slot and assignments.</summary>
        ProfileDocument RenameNativeController(ProfileDocument profile, string logicalDeviceId, string nativeName);
        #endregion END Public
    }
}
