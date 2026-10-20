using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Creates a game-owned controller reference independently of shared artwork and physical-unit pins.</summary>
    public interface ISimDeviceEditor
    {
        #region BEGIN Public
        /// <summary>Returns a validated independent profile with a new supported product reference.</summary>
        /// <param name="profile">Profile whose captured references must remain intact.</param>
        /// <param name="observed">Native product evidence; not a game index or entitlement.</param>
        /// <returns>A new profile with an unassigned logical controller.</returns>
        ProfileDocument AddDevice(ProfileDocument profile, DetectedDcsDevice observed);
        #endregion END Public
    }
}
