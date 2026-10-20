using Heimdall.Domain;

namespace Heimdall.Dcs.SnapshotModels
{
    /// <summary>Represents configuration modifier dto.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Device">The device.</param>
    /// <param name="Key">The key.</param>
    /// <param name="Mode">The mode.</param>
    internal sealed record ConfigurationModifierDto(string Name, string Device, string Key, ModifierMode Mode);
}
