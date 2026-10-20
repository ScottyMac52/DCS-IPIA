using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Checks native product compatibility without claiming a unique instance or entitlement.</summary>
    public interface ISimDeviceReferenceMatcher
    {
        #region BEGIN Public
        /// <summary>Checks whether a captured game reference can address an observed native product.</summary>
        /// <param name="reference">Captured game identity.</param>
        /// <param name="observed">Authoritative Windows device observation.</param>
        /// <returns>True only for a recognized compatible product reference.</returns>
        bool Matches(PhysicalDeviceIdentity reference, DetectedDcsDevice observed);
        #endregion END Public
    }
}
