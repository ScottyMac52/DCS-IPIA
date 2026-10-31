using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Builds the Dagor DirectInput mapping from exact product evidence and original object enumeration, never device order.</summary>
    public sealed record WarThunderPhysicalMap(IReadOnlyList<WarThunderPhysicalInput> Inputs, string Message)
    {
        #region BEGIN Public
        /// <summary>Validates saved counts against raw axes/buttons/hats before exposing physical assignment targets.</summary>
        public static WarThunderPhysicalMap Create(ProfileDocument profile, string deviceId, ControllerObservation observation)
        {
            var devices = WarThunderProfileBackend.ReadDevices(profile);
            var native = devices.Single(device => device.Id == deviceId);
            var identity = profile.Devices.Single(device => device.LogicalId == deviceId).Identity;
            var capabilities = observation.Capabilities;
            if (!native.Valid || !observation.Device.Connected || identity is null
                || !new WarThunderDeviceReferenceMatcher().Matches(identity, observation.Device)) return Missing("Link and connect the matching physical controller.");
            if (capabilities?.NativeObjects is null) return Missing("Rescan to capture the controller's native input order.");
            var axes = capabilities.NativeObjects.Where(item => (item.NativeType & 3) != 0).ToArray();
            var buttons = capabilities.Inputs.Where(input => input.Kind == ControllerInputKind.Button).ToArray();
            var hats = capabilities.Inputs.Where(input => input.Kind == ControllerInputKind.Hat).OrderBy(input => Instance(input.Evidence)).ToArray();
            var rawButtons = capabilities.NativeObjects.Count(item => (item.NativeType & 12) != 0);
            var rawHats = capabilities.NativeObjects.Count(item => (item.NativeType & 255) == 16);
            if (native.AxesCount != axes.Length + 2 * hats.Length || native.ButtonsCount != buttons.Length + 4 * hats.Length
                || rawButtons != buttons.Length || rawHats != hats.Length
                || !buttons.Select(input => Instance(input.Evidence)).Order().SequenceEqual(Enumerable.Range(0, buttons.Length))
                || !hats.Select(input => Instance(input.Evidence)).SequenceEqual(Enumerable.Range(0, hats.Length)))
                return Missing("Saved input counts differ from the connected controller. Native records remain available in the assignment table.");
            var inputs = new List<WarThunderPhysicalInput>();
            for (var index = 0; index < axes.Length; index++)
            {
                var mapped = capabilities.Inputs.Where(input => input.Kind == ControllerInputKind.Axis && input.Evidence == axes[index]).ToArray();
                if (mapped.Length != 1) return Missing("An axis has unsupported or ambiguous native object evidence.");
                inputs.Add(new(mapped[0].DcsKey, CommandType.Axis, index));
            }
            inputs.AddRange(buttons.Select(input => new WarThunderPhysicalInput(input.DcsKey, CommandType.Button, Instance(input.Evidence))));
            string[] directions = ["U", "R", "D", "L"];
            for (var hat = 0; hat < hats.Length; hat++)
                for (var direction = 0; direction < directions.Length; direction++)
                {
                    var key = hats[hat].DcsKey + "_" + directions[direction];
                    inputs.Add(new(key, CommandType.Button, buttons.Length + hat * 4 + direction));
                    inputs.Add(new(key, CommandType.Axis, axes.Length + hat * 2 + (direction % 2 == 0 ? 1 : 0)));
                }
            if (inputs.Any(input => devices.Count(device => device.Contains(input.Type, native.Encode(input.Type, input.Index))) != 1))
                return Missing("Saved input ranges overlap; physical assignment is unavailable until native ownership is unambiguous.");
            return new(inputs, "Click a control and Apply assignment, or drag a command onto it.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static int Instance(ControllerObject item) => (int)((item.NativeType >> 8) & 65535);
        private static WarThunderPhysicalMap Missing(string message) => new([], message);
        #endregion END private helpers
    }
}
