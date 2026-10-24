using System.Globalization;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Projects only verified joystick records while preserving every other record in pinned native evidence.</summary>
    internal static class SquadronsProfileProjection
    {
        #region BEGIN Public
        /// <summary>Creates slot-scoped logical devices without claiming saved names identify connected physical instances.</summary>
        public static ProfileDocument Create(string context, SquadronsNativeDocument document)
        {
            var profile = new ProfileDocument { BackendId = "star-wars-squadrons", ContextId = context, ProfileId = "squadrons/" + context, Name = "Squadrons Controls" };
            foreach (var (key, value) in document.Values.Where(x => x.Key.StartsWith("GstInput.JoystickDevice", StringComparison.Ordinal)))
            {
                if (!int.TryParse(key["GstInput.JoystickDevice".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot is < 1 or > 10)
                    throw new InvalidOperationException("Unsupported Squadrons saved joystick slot.");
                if (string.IsNullOrWhiteSpace(value)) continue;
                var device = new LogicalDevice
                {
                    LogicalId = "squadrons.joystick." + slot, ContextId = context,
                    Identity = new() { WindowsName = value, SimSpecificIds = new() { ["squadrons.slot"] = slot.ToString(CultureInfo.InvariantCulture) } }
                };
                foreach (var row in document.Bindings.Where(x => x.Type == 3 && x.DeviceId == slot - 1 && x.Fields["modifier"] == 0 && x.Fields["altbutton"] == 0))
                    Add(device, row, context);
                profile.Devices.Add(device);
            }
            return profile;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Add(LogicalDevice device, SquadronsBindingRow row, string context)
        {
            if (row.Input is not { } input) return;
            var extensions = new Dictionary<string, object> { ["squadrons.record"] = row.RecordId,
                ["squadrons.direction"] = input.Direction.ToString(CultureInfo.InvariantCulture),
                ["squadrons.negate"] = row.Fields["negate"].ToString(CultureInfo.InvariantCulture) };
            if (input.Type == CommandType.Axis)
                device.Axes.Add(new() { CommandId = context + "/" + row.Concept, SourceAxis = input.Key, SimSpecific = extensions });
            else
                device.Buttons.Add(new() { CommandId = context + "/" + row.Concept, SourceButton = input.Key, Mode = BindingMode.Press, SimSpecific = extensions });
        }
        #endregion END private helpers
    }
}
