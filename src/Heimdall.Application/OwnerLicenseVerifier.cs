using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Application
{
    /// <summary>Validates signed Premium and Owner entitlements without trusting identifiers or client-side flags.</summary>
    public sealed class OwnerLicenseVerifier
    {
        private readonly string _publicKey;
        private static readonly JsonSerializerOptions Options = new() { MaxDepth = 4, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        /// <summary>Uses the pinned application key, or an explicitly supplied verification key for issuer/tests.</summary>
        public OwnerLicenseVerifier(string publicKey = OwnerLicensePublicKey.Pem) => _publicKey = publicKey;
        #region BEGIN Public
        /// <summary>Maximum encoded license file size.</summary>
        public const int MaximumBytes = 16384;
        /// <summary>Returns authenticated edition claims, or null for malformed, foreign or tampered licenses.</summary>
        public OwnerLicenseClaims? Verify(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes) return null;
            try
            {
                RequireUniqueObject(json);
                var envelope = JsonSerializer.Deserialize<SignedOwnerLicense>(json, Options)!;
                var payload = Convert.FromBase64String(envelope.Payload);
                var signature = Convert.FromBase64String(envelope.Signature);
                using var rsa = RSA.Create(); rsa.ImportFromPem(_publicKey);
                if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) return null;
                var text = new UTF8Encoding(false, true).GetString(payload);
                RequireUniqueObject(text);
                var claims = JsonSerializer.Deserialize<OwnerLicenseClaims>(text, Options)!;
                return claims.SchemaVersion == 1 && (claims.Product == "HEIMDALL" || claims.Product == "DCS-IPIA") && (claims.Edition is "Owner" or "Premium") &&
                    claims.LicenseId != Guid.Empty && !string.IsNullOrWhiteSpace(claims.Owner) && claims.Owner.Length <= 120 ? claims : null;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or CryptographicException or DecoderFallbackException)
            { return null; }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void RequireUniqueObject(string json)
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Expected license object.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in doc.RootElement.EnumerateObject())
                if (!keys.Add(property.Name)) throw new JsonException("Duplicate license field.");
        }
        #endregion END private helpers
    }
}
