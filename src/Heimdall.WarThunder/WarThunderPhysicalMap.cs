using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Projects independently validated native layout slots onto compatibility drawing aliases.</summary>
    public sealed record WarThunderPhysicalMap(IReadOnlyList<WarThunderPhysicalInput> Inputs, string Message)
    {
        #region BEGIN Public
        /// <summary>Gets the complete neutral-to-native inclusion trace for advanced review.</summary>
        public WarThunderInputLayout? Layout { get; init; }
        /// <summary>Validates button-slot and axis-slot mappings independently; saved ranges are never renumbered.</summary>
        public static WarThunderPhysicalMap Create(ProfileDocument profile, string deviceId, ControllerObservation observation)
        {
            var devices = WarThunderProfileBackend.ReadDevices(profile);
            var native = devices.Single(device => device.Id == deviceId);
            var identity = profile.Devices.Single(device => device.LogicalId == deviceId).Identity;
            if (!native.Valid || !observation.Device.Connected || identity is null
                || !new WarThunderDeviceReferenceMatcher().Matches(identity, observation.Device)) return Missing("Link and connect the matching physical controller.");
            if (observation.Capabilities?.NativeObjects is null) return Missing(observation.Error ?? "Rescan to capture the controller's native input order.");
            var catalog = observation.Capabilities.ReportedInputs;
            var layout = WarThunderInputLayout.Create(catalog);
            var aliases = ControllerDrawingAliases.Create(catalog);
            var errors = new List<string>();
            var inputs = ReadGroup(layout.Axes, native.AxesCount, layout.AxesComplete, CommandType.Axis, aliases, errors);
            inputs.AddRange(ReadGroup(layout.Buttons, native.ButtonsCount, layout.ButtonsComplete, CommandType.Button, aliases, errors));
            if (inputs.Any(input => devices.Count(device => device.Contains(input.Type, native.Encode(input.Type, input.Index))) != 1))
                return Missing("Saved input ranges overlap; physical assignment is unavailable until native ownership is unambiguous.") with { Layout = layout };
            var summary = string.Create(CultureInfo.InvariantCulture,
                $"Translated {layout.Buttons.Count} native button slots and {layout.Axes.Count} native axis slots. Drawing aliases available: {inputs.Count(input => input.Type == CommandType.Button)} button/POV, {inputs.Count(input => input.Type == CommandType.Axis)} axis/POV. Driver slots are not confirmed physical controls.");
            return new(inputs, summary + " " + string.Join(" ", errors)
                + (inputs.Count > 0 ? " Click a compatible control and Apply assignment, or drag a command onto it." : " Native records remain available in the assignment table.")) { Layout = layout };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static List<WarThunderPhysicalInput> ReadGroup(IReadOnlyList<WarThunderInputSlot> slots, int expected,
            bool complete, CommandType type, IReadOnlyDictionary<string, string> aliases, List<string> errors)
        {
            var kind = type == CommandType.Axis ? "Axis" : "Button";
            if (!complete || expected != slots.Count)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{kind} mapping blocked: BLK declares {expected}; translated driver layout contains {slots.Count}. Missing, unsupported or ambiguous evidence requires review of the native input translation trace."));
                return [];
            }
            return slots.SelectMany(slot => WarThunderDrawingAliases.ForSlot(slot, aliases).Select(key => new WarThunderPhysicalInput(key, type, slot.NativeIndex))).ToList();
        }
        private static WarThunderPhysicalMap Missing(string message) => new([], message);
        #endregion END private helpers
    }
}
