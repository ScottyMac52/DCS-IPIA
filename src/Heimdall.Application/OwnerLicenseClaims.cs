namespace Heimdall.Application
{
    /// <summary>Signed offline Premium or Owner entitlement; the historical wire format remains compatible.</summary>
    /// <param name="SchemaVersion">Supported claims schema.</param>
    /// <param name="Product">Exact application product.</param>
    /// <param name="Edition">Exact Premium or Owner entitlement identifier.</param>
    /// <param name="LicenseId">Unique issuance identifier.</param>
    /// <param name="Owner">Licensed owner's display name.</param>
    public sealed record OwnerLicenseClaims(int SchemaVersion, string Product, string Edition, Guid LicenseId, string Owner);
}
