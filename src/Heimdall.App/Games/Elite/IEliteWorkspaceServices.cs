using Heimdall.EliteDangerous;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Elite Dangerous-only native services, kept at the ED edge instead of the shared game service composition.</summary>
    internal interface IEliteWorkspaceServices
    {
        #region BEGIN Public
        /// <summary>Gets the ED profile backend.</summary>
        EliteDangerousProfileBackend Backend { get; }
        /// <summary>Gets native product-reference validation, separate from physical instance resolution.</summary>
        EliteDeviceReferenceMatcher DeviceReferences { get; }
        #endregion END Public
    }
}
