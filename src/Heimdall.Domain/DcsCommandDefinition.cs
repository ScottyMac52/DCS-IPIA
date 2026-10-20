namespace Heimdall.Domain
{
    /// <summary>Represents DCS command definition.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="CatalogId">The catalog identifier.</param>
    /// <param name="DcsCommandId">The DCS command identifier.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Category">The category.</param>
    /// <param name="IsAxis">The is axis.</param>
    public sealed record DcsCommandDefinition(Guid Id, Guid CatalogId, string DcsCommandId, string Name,
                                              string Category, bool IsAxis);
}
