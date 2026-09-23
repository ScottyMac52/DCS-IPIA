namespace Dcs.Ipia.Domain;

/// <summary>Represents physical device.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="StableKey">The stable key.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Aliases">The aliases.</param>
public sealed record PhysicalDevice(Guid Id,string StableKey,string DisplayName,IReadOnlyList<string> Aliases);
