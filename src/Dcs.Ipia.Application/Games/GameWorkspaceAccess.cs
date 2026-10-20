using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Application.Games
{
    /// <summary>Applies the existing edition/manufacturer boundary independently of backend/native metadata.</summary>
    public static class GameWorkspaceAccess
    {
        #region BEGIN Public
        /// <summary>Requires all physical references to have reviewed eligible associations in Free.</summary>
        public static void Require(GameWorkspace workspace, EditionPolicy policy, IReadOnlyCollection<string> catalogIds,
            IReadOnlyList<DetectedDcsDevice> observed, ISimDeviceReferenceMatcher? referenceMatcher)
        {
            if (policy.IsOwner) return;
            foreach (var device in workspace.Profile.Devices.Where(x => !IsSystemDevice(x.Identity?.SimSpecificIds.GetValueOrDefault("ed.device"))))
            {
                if (!workspace.Hardware.TryGetValue(device.LogicalId, out var pin) || !catalogIds.Contains(pin.HardwareId)
                    || !policy.AllowsHardware(pin.HardwareId) || !policy.AllowsDevice(pin.Device)
                    || referenceMatcher?.Matches(device.Identity!, pin.Device) != true)
                    throw new InvalidOperationException("Associate every controller with reviewed Thrustmaster hardware before editing or exporting in Free Edition. Imported data is retained read-only; Premium availability TBA.");
                var current = observed.Where(x => x.Connected && x.PhysicalGuid == pin.Device.PhysicalGuid).ToArray();
                if (current.Length > 1 || current.Any(x => !policy.AllowsDevice(x)))
                    throw new InvalidOperationException("Controller manufacturer evidence is ambiguous or is not eligible in Free Edition.");
            }
        }
        /// <summary>Recognizes only the native keyboard/mouse tokens, never controller name aliases.</summary>
        public static bool IsSystemDevice(string? token) => token is "Keyboard" or "Mouse";
        #endregion END Public
    }
}
