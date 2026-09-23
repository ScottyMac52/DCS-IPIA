namespace Dcs.Ipia.Domain;

/// <summary>Represents UI Layer tombstone.</summary>
/// <param name="ModuleId">The module identifier.</param>
/// <param name="StableKey">The stable key.</param>
/// <param name="Reason">The reason.</param>
public sealed record UiLayerTombstone(Guid ModuleId,string StableKey,string Reason);
