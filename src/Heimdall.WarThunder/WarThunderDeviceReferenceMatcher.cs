using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Matches saved VID/PID evidence only; selecting a unique instance remains an explicit user operation.</summary>
    public sealed class WarThunderDeviceReferenceMatcher : ISimDeviceReferenceMatcher
    {
        #region BEGIN Public
        /// <summary>Compares captured VID/PID with authoritative native product evidence.</summary>
        public bool Matches(PhysicalDeviceIdentity reference, DetectedDcsDevice observed)
        {
            ArgumentNullException.ThrowIfNull(reference); ArgumentNullException.ThrowIfNull(observed);
            if (!observed.ProductGuid.ToString("D").EndsWith("504944564944", StringComparison.OrdinalIgnoreCase)) return false;
            var bytes = observed.ProductGuid.ToByteArray();
            return reference.VendorId != 0 && reference.ProductId != 0
                && reference.VendorId == (bytes[0] | bytes[1] << 8) && reference.ProductId == (bytes[2] | bytes[3] << 8);
        }
        #endregion END Public
    }
}
