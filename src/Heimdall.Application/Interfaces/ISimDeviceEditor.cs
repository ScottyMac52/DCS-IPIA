using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
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
        /// <summary>Removes a controller and clears every native binding that depends on it.</summary>
        ProfileDocument RemoveDevice(ProfileDocument profile, string logicalId);
        /// <summary>Replaces references using verified target input keys, clearing unsupported whole bindings.</summary>
        ProfileDocument ReplaceDevice(ProfileDocument profile, string logicalId, string targetId, IReadOnlyCollection<string> nativeInputs);
        #endregion END Public
    }
}
