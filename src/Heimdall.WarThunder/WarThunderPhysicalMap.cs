using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Builds independent axis and button maps from exact product evidence and original object enumeration.</summary>
    public sealed record WarThunderPhysicalMap(IReadOnlyList<WarThunderPhysicalInput> Inputs, string Message)
    {
        #region BEGIN Public
        /// <summary>Retains verified buttons when axes cannot be mapped, and vice versa; native ranges are never renumbered.</summary>
        public static WarThunderPhysicalMap Create(ProfileDocument profile, string deviceId, ControllerObservation observation)
        {
            var devices = WarThunderProfileBackend.ReadDevices(profile);
            var native = devices.Single(device => device.Id == deviceId);
            var identity = profile.Devices.Single(device => device.LogicalId == deviceId).Identity;
            var capabilities = observation.Capabilities;
            if (!native.Valid || !observation.Device.Connected || identity is null
                || !new WarThunderDeviceReferenceMatcher().Matches(identity, observation.Device)) return Missing("Link and connect the matching physical controller.");
            if (capabilities?.NativeObjects is null) return Missing(observation.Error ?? "Rescan to capture the controller's native input order.");
            var axes = capabilities.NativeObjects.Where(item => (item.NativeType & 3) != 0).ToArray();
            var hats = capabilities.Inputs.Where(input => input.Kind == ControllerInputKind.Hat).OrderBy(input => Instance(input.Evidence)).ToArray();
            if (capabilities.NativeObjects.Count(item => (item.NativeType & 255) == 16) != hats.Length
                || !hats.Select(input => Instance(input.Evidence)).SequenceEqual(Enumerable.Range(0, hats.Length)))
                return Missing("Native POV objects are unsupported or ambiguous. Rescan the connected controller.");
            var errors = new List<string>();
            var inputs = ReadAxes(native, capabilities, axes, hats, errors);
            inputs.AddRange(ReadButtons(native, capabilities, hats, errors));
            if (inputs.Any(input => devices.Count(device => device.Contains(input.Type, native.Encode(input.Type, input.Index))) != 1))
                return Missing("Saved input ranges overlap; physical assignment is unavailable until native ownership is unambiguous.");
            var summary = string.Create(CultureInfo.InvariantCulture,
                $"Verified {inputs.Count(input => input.Type == CommandType.Button)} button/POV targets and {inputs.Count(input => input.Type == CommandType.Axis)} axis/POV targets.");
            return new(inputs, summary + " " + string.Join(" ", errors)
                + (inputs.Count > 0 ? " Click a compatible control and Apply assignment, or drag a command onto it." : " Native records remain available in the assignment table."));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static List<WarThunderPhysicalInput> ReadAxes(WarThunderDevice native, ControllerCapabilities capabilities,
            ControllerObject[] axes, ControllerInput[] hats, List<string> errors)
        {
            var count = axes.Length + 2 * hats.Length;
            if (native.AxesCount != count)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Axis mapping blocked: BLK declares {native.AxesCount}; connected controller exposes {axes.Length} axes plus {hats.Length} POV(s), totaling {count}."));
                return [];
            }
            var inputs = new List<WarThunderPhysicalInput>();
            for (var index = 0; index < axes.Length; index++)
            {
                var mapped = capabilities.Inputs.Where(input => input.Kind == ControllerInputKind.Axis && input.Evidence == axes[index]).ToArray();
                if (mapped.Length != 1)
                {
                    errors.Add("Axis mapping blocked: an enumerated axis has unsupported or ambiguous object evidence.");
                    return [];
                }
                inputs.Add(new(mapped[0].DcsKey, CommandType.Axis, index));
            }
            AddHatTargets(inputs, hats, axes.Length, CommandType.Axis);
            return inputs;
        }
        private static List<WarThunderPhysicalInput> ReadButtons(WarThunderDevice native, ControllerCapabilities capabilities,
            ControllerInput[] hats, List<string> errors)
        {
            var buttons = capabilities.Inputs.Where(input => input.Kind == ControllerInputKind.Button).ToArray();
            var count = buttons.Length + 4 * hats.Length;
            if (native.ButtonsCount != count || capabilities.NativeObjects!.Count(item => (item.NativeType & 12) != 0) != buttons.Length
                || !buttons.Select(input => Instance(input.Evidence)).Order().SequenceEqual(Enumerable.Range(0, buttons.Length)))
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Button mapping blocked: BLK declares {native.ButtonsCount}; connected controller exposes {buttons.Length} supported buttons plus {hats.Length} POV(s), totaling {count}; raw instances must be complete and unique."));
                return [];
            }
            var inputs = buttons.Select(input => new WarThunderPhysicalInput(input.DcsKey, CommandType.Button, Instance(input.Evidence))).ToList();
            AddHatTargets(inputs, hats, buttons.Length, CommandType.Button);
            return inputs;
        }
        private static void AddHatTargets(List<WarThunderPhysicalInput> inputs, ControllerInput[] hats, int offset, CommandType type)
        {
            string[] directions = ["U", "R", "D", "L"];
            for (var hat = 0; hat < hats.Length; hat++)
                for (var direction = 0; direction < directions.Length; direction++)
                {
                    var index = type == CommandType.Button ? offset + hat * 4 + direction : offset + hat * 2 + (direction % 2 == 0 ? 1 : 0);
                    inputs.Add(new(hats[hat].DcsKey + "_" + directions[direction], type, index));
                }
        }
        private static int Instance(ControllerObject item) => (int)((item.NativeType >> 8) & 65535);
        private static WarThunderPhysicalMap Missing(string message) => new([], message);
        #endregion END private helpers
    }
}
