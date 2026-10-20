using Heimdall.Application;
using Heimdall.Application.Interfaces;
using System.Runtime.InteropServices;

namespace Heimdall.App
{
    /// <summary>Enumerates attached DirectInput controllers using native instance GUIDs.</summary>
    public sealed class WindowsConnectedDeviceDiscovery : IConnectedDeviceDiscovery, IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Reads devices without acquiring them or changing DCS files.</summary>
        /// <returns>Native attached controller instances.</returns>
        public IReadOnlyList<DetectedDcsDevice> Discover()
        {
            using var connection = new WindowsDirectInputConnection();
            return connection.Discover();
        }
        /// <inheritdoc/>
        public Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default) =>
            Task.Run<IReadOnlyList<ControllerObservation>>(() => ReadCapabilities(ct), ct);
        #endregion END Public
        #region BEGIN private helpers
        private static unsafe List<ControllerObservation> ReadCapabilities(CancellationToken ct)
        {
            using var connection = new WindowsDirectInputConnection();
            var observed = new List<ControllerObservation>();
            foreach (var identity in connection.Discover(ct))
            {
                ct.ThrowIfCancellationRequested();
                nint device = 0;
                try
                {
                    device = connection.CreateDevice(identity.PhysicalGuid);
                    var capabilities = ControllerCapabilityMapper.Map(WindowsControllerObjectEnumeration.Read(device, ct));
                    observed.Add(new(identity, capabilities));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { observed.Add(new(identity, null, "Input enumeration failed: " + ex.Message)); }
                finally
                {
                    if (device != 0)
                        _ = ((delegate* unmanaged[Stdcall]<nint, uint>)Marshal.ReadIntPtr(Marshal.ReadIntPtr(device), 2 * nint.Size))(device);
                }
            }
            return observed;
        }
        #endregion END private helpers
    }
}
