namespace Heimdall.Domain
{
    /// <summary>Preserves an ordered DCS profile filename mapping rule from shared hardware.</summary>
    /// <param name="DeviceKey">The canonical device key or alias.</param>
    /// <param name="Patterns">Filename fragments in their original precedence order.</param>
    /// <param name="CanonicalName">An optional canonical DCS name.</param>
    /// <param name="Source">An optional rule provenance marker.</param>
    public sealed record DeviceMappingRule(string DeviceKey, IReadOnlyList<string> Patterns, string? CanonicalName,
                                           string? Source);
}
