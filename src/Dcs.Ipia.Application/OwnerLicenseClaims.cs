namespace Dcs.Ipia.Application
{
    /// <summary>Signed owner-only entitlement with no expiry or customer payment semantics.</summary>
    /// <param name="SchemaVersion">Supported claims schema.</param>
    /// <param name="Product">Exact application product.</param>
    /// <param name="Edition">Owner entitlement identifier.</param>
    /// <param name="LicenseId">Unique issuance identifier.</param>
    /// <param name="Owner">Licensed owner's display name.</param>
    public sealed record OwnerLicenseClaims(int SchemaVersion, string Product, string Edition, Guid LicenseId, string Owner);
}
