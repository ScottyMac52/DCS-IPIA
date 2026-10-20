using System.Globalization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Checks explicit VID/PID references and reviewed built-in aliases against native HID product evidence.</summary>
    public sealed class EliteDeviceReferenceMatcher : ISimDeviceReferenceMatcher
    {
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
        {
            ["ThrustMasterWarthogJoystick"] = "044F0402",
            ["ThrustMasterWarthogThrottle"] = "044F0404"
        };
        #region BEGIN Public
        /// <summary>Compares products only; repeated units still require explicit exact-instance association.</summary>
        public bool Matches(PhysicalDeviceIdentity reference, DetectedDcsDevice observed)
        {
            ArgumentNullException.ThrowIfNull(reference);
            ArgumentNullException.ThrowIfNull(observed);
            if (reference.SimSpecificIds is null || !reference.SimSpecificIds.TryGetValue("ed.device", out var token)) return false;
            return Normalize(token) is { } normalized && normalized == ProductToken(observed);
        }
        #endregion END Public
        #region BEGIN private helpers
        internal static string? Normalize(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            if (Aliases.TryGetValue(token, out var alias)) token = alias;
            return token.Length == 8 && token.All(Uri.IsHexDigit) ? token.ToUpperInvariant() : null;
        }
        internal static string? ProductToken(DetectedDcsDevice observed)
        {
            if (!observed.ProductGuid.ToString("D").EndsWith("504944564944", StringComparison.OrdinalIgnoreCase)) return null;
            var bytes = observed.ProductGuid.ToByteArray();
            var vendor = bytes[0] | bytes[1] << 8;
            var product = bytes[2] | bytes[3] << 8;
            return vendor == 0 || product == 0 ? null : vendor.ToString("X4", CultureInfo.InvariantCulture) + product.ToString("X4", CultureInfo.InvariantCulture);
        }
        #endregion END private helpers
    }
}

