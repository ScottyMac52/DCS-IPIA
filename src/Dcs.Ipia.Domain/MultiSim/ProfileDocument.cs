namespace Dcs.Ipia.Domain.MultiSim
{
    /// <summary>Intermediate, sim-agnostic control profile.</summary>
    public sealed class ProfileDocument
    {
        #region BEGIN Public

        /// <summary>Gets or sets the profile name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the logical devices in this profile.</summary>
        public List<LogicalDevice> Devices { get; set; } = new();

        /// <summary>Gets or sets optional sim-specific metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        #endregion END Public
    }
}
