using System.Text.Json;

namespace Heimdall.Domain.MultiSim
{
    /// <summary>Intermediate, sim-agnostic control profile.</summary>
    public sealed class ProfileDocument
    {
        #region BEGIN Public

        /// <summary>Gets or sets the portable schema version; unsupported versions must be rejected.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Gets or sets the owning backend identifier.</summary>
        public string BackendId { get; set; } = string.Empty;

        /// <summary>Gets or sets the backend contract version.</summary>
        public int BackendVersion { get; set; } = 1;

        /// <summary>Gets or sets a stable profile identity, independent of its display name.</summary>
        public string ProfileId { get; set; } = string.Empty;

        /// <summary>Gets or sets the game module, vehicle or general context identifier.</summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>Gets or sets versioned JSON native evidence; adapters validate ownership and shape.</summary>
        public Dictionary<string, JsonElement> NativeData { get; set; } = new();

        /// <summary>Gets or sets the profile name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the logical devices in this profile.</summary>
        public List<LogicalDevice> Devices { get; set; } = new();

        /// <summary>Gets or sets optional sim-specific metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        #endregion END Public
    }
}

