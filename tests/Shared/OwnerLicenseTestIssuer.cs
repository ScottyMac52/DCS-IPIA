using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application;

namespace Heimdall.TestSupport
{
    /// <summary>Creates ephemeral test-only issuances; its key never authenticates against the production app.</summary>
    internal sealed class OwnerLicenseTestIssuer : IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);
        #region BEGIN Public
        /// <summary>Gets the temporary issuer's verification adapter.</summary>
        public OwnerLicenseVerifier Verifier => new(_key.ExportSubjectPublicKeyInfoPem());
        /// <summary>Gets an authenticated all-feature policy for isolated test fixtures.</summary>
        public EditionPolicy Policy => new(License(), Verifier);
        /// <summary>Signs complete claims, optionally with invalid fields for negative tests.</summary>
        public string License(OwnerLicenseClaims? claims = null) => Sign(JsonSerializer.Serialize(claims ??
            new OwnerLicenseClaims(1, "DCS-IPIA", "Owner", Guid.NewGuid(), "Isolated test owner")));
        /// <summary>Signs exact payload bytes, including malformed input for verification tests.</summary>
        public string Sign(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return JsonSerializer.Serialize(new SignedOwnerLicense(Convert.ToBase64String(bytes), Convert.ToBase64String(
                _key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))));
        }
        /// <summary>Destroys the temporary test signing key.</summary>
        public void Dispose() => _key.Dispose();
        #endregion END Public
    }
}
