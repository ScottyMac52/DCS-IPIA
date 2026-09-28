namespace Dcs.Ipia.Domain
{
    /// <summary>Represents command catalog.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="StableKey">The stable key.</param>
    /// <param name="Source">The source.</param>
    /// <param name="Version">The version.</param>
    public sealed record CommandCatalog(Guid Id, string StableKey, string Source, string Version);
}
