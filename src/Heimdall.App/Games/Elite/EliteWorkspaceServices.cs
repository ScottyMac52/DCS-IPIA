using Heimdall.EliteDangerous;

namespace Heimdall.App.Games.Elite
{
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
