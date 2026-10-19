namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// Intermediate, sim-agnostic representation of a control profile.
/// </summary>
public sealed class ProfileDocument
{
    /// <summary>Gets or sets the profile name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the logical devices contained in this profile.</summary>
    public List<LogicalDevice> Devices { get; set; } = new();

    /// <summary>Gets or sets optional sim-specific metadata.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
