using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Projects saved mapping identities; native records remain authoritative instead of inventing physical input names.</summary>
    internal static class WarThunderProfileProjection
    {
        #region BEGIN Public
        /// <summary>Creates a portable envelope whose native index space cannot be confused with DirectInput control names.</summary>
        internal static ProfileDocument Create(WarThunderNativeEvidence evidence, WarThunderNativeProfile native)
        {
            var profile = new ProfileDocument
            {
                BackendId = "war-thunder", BackendVersion = 1, ContextId = evidence.ContextId,
                ProfileId = "war-thunder/" + evidence.ContextId, Name = Path.GetFileNameWithoutExtension(evidence.FileName),
                Metadata = new()
                {
                    ["war-thunder.input-space"] = "native-offset-indices", ["war-thunder.catalog-coverage"] = "captured",
                    ["war-thunder.source-sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.Text))),
                    ["war-thunder.catalog-sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.CatalogText)))
                }
            };
            foreach (var device in native.Devices)
            {
                var product = device.Product.Split(':');
                ushort vendor = 0; ushort model = 0;
                if (product.Length == 2)
                {
                    ushort.TryParse(product[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vendor);
                    ushort.TryParse(product[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out model);
                }
                profile.Devices.Add(new LogicalDevice
                {
                    LogicalId = device.Id, ContextId = evidence.ContextId,
                    Identity = new PhysicalDeviceIdentity
                    {
                        WindowsName = device.Name, VendorId = vendor, ProductId = model,
                        SimSpecificIds = new()
                        {
                            ["war-thunder.mapping"] = device.RecordId, ["war-thunder.devId"] = device.Product,
                            ["war-thunder.axesOffset"] = device.AxesOffset.ToString(CultureInfo.InvariantCulture),
                            ["war-thunder.axesCount"] = device.AxesCount.ToString(CultureInfo.InvariantCulture),
                            ["war-thunder.buttonsOffset"] = device.ButtonsOffset.ToString(CultureInfo.InvariantCulture),
                            ["war-thunder.buttonsCount"] = device.ButtonsCount.ToString(CultureInfo.InvariantCulture)
                        }
                    }
                });
            }
            return profile;
        }
        #endregion END Public
    }
}
