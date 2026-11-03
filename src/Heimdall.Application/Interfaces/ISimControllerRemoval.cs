using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Opts an adapter into reviewed native controller removal with explicit surviving identity mapping.</summary>
    public interface ISimControllerRemoval
    {
        #region BEGIN Public
        /// <summary>Removes native dependencies and reports how surviving logical references follow their controllers.</summary>
        SimControllerRemoval RemoveController(ProfileDocument profile, string logicalId);
        #endregion END Public
    }
}
