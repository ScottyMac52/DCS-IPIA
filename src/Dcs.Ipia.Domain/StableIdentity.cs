using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain
{
    /// <summary>Creates GUID-independent stable identities.</summary>
    public static class StableIdentity
    {
        #region BEGIN Public
        /// <summary>Creates a stable identity from its scope and value.</summary>
        /// <param name="scope">The scope.</param>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public static Guid Create(string scope, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{scope.Trim()}\0{value.Trim()}"));
            Span<byte> guid = stackalloc byte[16];
            bytes.AsSpan(0, 16).CopyTo(guid);
            guid[6] = (byte)((guid[6] & 0x0f) | 0x50);
            guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
            return new Guid(guid);
        }
        #endregion END Public
    }
}
