using System.Globalization;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
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
            if (!observed.ProductGuid.ToString("D").EndsWith("504944564944", StringComparison.OrdinalIgnoreCase)
                || reference.SimSpecificIds is null || !reference.SimSpecificIds.TryGetValue("ed.device", out var token)
                || string.IsNullOrWhiteSpace(token)) return false;
            if (Aliases.TryGetValue(token, out var alias)) token = alias;
            if (token.Length != 8 || !token.All(Uri.IsHexDigit)) return false;
            var bytes = observed.ProductGuid.ToByteArray();
            var vendor = bytes[0] | bytes[1] << 8;
            var product = bytes[2] | bytes[3] << 8;
            return token.Equals(vendor.ToString("X4", CultureInfo.InvariantCulture) + product.ToString("X4", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }
        #endregion END Public
    }
}
