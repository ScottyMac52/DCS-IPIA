using System.Text.Json;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises cryptographic verification, bounded parsing and atomic per-user activation storage.</summary>
    public sealed class OwnerLicenseTests
    {
        #region BEGIN Public
        /// <summary>Both exact product names authenticate after signature verification; arbitrary products do not.</summary>
        [Theory]
        [InlineData("HEIMDALL", true)]
        [InlineData("DCS-IPIA", true)]
        [InlineData("Other", false)]
        public void Product_rename_preserves_existing_signed_licenses(string product, bool accepted)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var license = issuer.License(new OwnerLicenseClaims(1, product, "Owner", Guid.NewGuid(), "Test owner"));
            Assert.Equal(accepted, issuer.Verifier.Verify(license) is not null);
        }

        /// <summary>Malformed identifiers and serialized flags never substitute for a signature.</summary>
        [Theory]
        [InlineData("")][InlineData(" ")][InlineData("null")][InlineData("[]")][InlineData("{}")][InlineData("{")]
        [InlineData("{\"Payload\":\"invalid\",\"Signature\":\"invalid\"}")]
        [InlineData("{\"Payload\":null,\"Signature\":null}")]
        [InlineData("{\"Payload\":\"\",\"Payload\":\"\",\"Signature\":\"\"}")]
        [InlineData("{\"IsOwner\":true}")]
        public void Invalid_documents_are_Free(string document) => Assert.Null(new OwnerLicenseVerifier().Verify(document));
        /// <summary>Valid signatures still require the exact product, schema and Owner entitlement.</summary>
        [Fact]
        public void Authentication_rejects_tampering_foreign_keys_and_invalid_claims()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var other = new OwnerLicenseTestIssuer();
            var claims = new OwnerLicenseClaims(1, "DCS-IPIA", "Owner", Guid.NewGuid(), "Test owner");
            Assert.Equal(claims, issuer.Verifier.Verify(issuer.License(claims)));
            Assert.Null(other.Verifier.Verify(issuer.License()));
            foreach (var invalid in new[] { claims with { SchemaVersion = 2 }, claims with { Product = "Other" },
                claims with { Edition = "Premium" }, claims with { LicenseId = Guid.Empty }, claims with { Owner = " " },
                claims with { Owner = new string('x', 121) } })
                Assert.Null(issuer.Verifier.Verify(issuer.License(invalid)));
            foreach (var text in new[] { "null", "[]", "{", "{\"SchemaVersion\":1,\"SchemaVersion\":1}", "{\"Unknown\":true}" })
                Assert.Null(issuer.Verifier.Verify(issuer.Sign(text)));
            var envelope = JsonSerializer.Deserialize<SignedOwnerLicense>(issuer.License())!;
            Assert.Null(issuer.Verifier.Verify(JsonSerializer.Serialize(envelope with { Payload = Convert.ToBase64String("changed"u8.ToArray()) })));
            Assert.Null(issuer.Verifier.Verify(new string('x', OwnerLicenseVerifier.MaximumBytes + 1)));
            Assert.Null(new OwnerLicenseVerifier("not a public key").Verify(issuer.License()));
        }
        /// <summary>Activation survives restart and invalid/oversized/failed imports preserve accepted bytes.</summary>
        [Fact]
        public void Store_validates_before_atomic_replacement_and_falls_back_safely()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-license-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                using var issuer = new OwnerLicenseTestIssuer();
                var target = Path.Combine(root, "private", "owner.json"); var source = Path.Combine(root, "input.json");
                var store = new FileOwnerLicenseStore(target, issuer.Verifier);
                Assert.False(store.Load().IsOwner); Assert.Null(store.LastError);
                var original = issuer.License(); File.WriteAllText(source, original);
                Assert.True(store.Import(source)); Assert.True(new FileOwnerLicenseStore(target, issuer.Verifier).Load().IsOwner);
                File.WriteAllText(source, "{}"); Assert.False(store.Import(source)); Assert.Equal(original, File.ReadAllText(target));
                File.WriteAllText(source, new string('x', OwnerLicenseVerifier.MaximumBytes + 1)); Assert.False(store.Import(source));
                File.WriteAllText(target, "{}"); Assert.False(store.Load().IsOwner); Assert.NotNull(store.LastError);
                File.WriteAllText(source, original); File.Delete(target); Directory.CreateDirectory(target);
                Assert.False(store.Import(source)); Assert.NotNull(store.LastError);
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(target)!, "*.tmp"));
                Assert.False(store.Import(Path.Combine(root, "missing.json")));
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
    }
}
