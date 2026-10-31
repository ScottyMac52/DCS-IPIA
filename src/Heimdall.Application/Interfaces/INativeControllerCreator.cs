using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Opts a simulator into creating a native controller declaration without a prior in-game binding.</summary>
    public interface INativeControllerCreator
    {
        #region BEGIN Public
        /// <summary>Adds one named controller in an unused native slot while retaining all existing assignments.</summary>
        ProfileDocument AddNativeController(ProfileDocument profile, string nativeName);
        #endregion END Public
    }
}
