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

    /// <summary>Default ED native services.</summary>
    internal sealed class EliteWorkspaceServices : IEliteWorkspaceServices
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public EliteDangerousProfileBackend Backend { get; } = new();
        /// <inheritdoc/>
        public EliteDeviceReferenceMatcher DeviceReferences { get; } = new();
        #endregion END Public
    }
}
