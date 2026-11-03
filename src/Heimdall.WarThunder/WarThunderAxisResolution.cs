namespace Heimdall.WarThunder
{
    /// <summary>Driver axis-storage selection; a missing key may mean an excluded object or insufficient evidence.</summary>
    internal readonly record struct WarThunderAxisResolution(string? StorageKey, string Detail, bool Complete);
}
