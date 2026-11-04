using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Appends a native mapping without moving existing controller ranges or bindings.</summary>
    internal static class WarThunderControllerAddition
    {
        internal static string Create(WarThunderNativeProfile native, string name, ControllerObservation? observation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name.Length > 256 || name.Any(char.IsControl))
                throw new InvalidOperationException("Use a controller name of at most 256 characters without control characters.");
            if (observation is not { Device.Connected: true, Error: null, Capabilities: { } capabilities }
                || !capabilities.ReportedInputs.HasNativeDetails)
                throw new InvalidOperationException("Rescan the connected controller to capture its native input layout.");
            var product = observation.Device.ProductGuid;
            if (!product.ToString("D").EndsWith("504944564944", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The connected controller has no verified DirectInput VID/PID identity.");
            var catalog = capabilities.ReportedInputs;
            var layout = WarThunderInputLayout.Create(catalog);
            if (!layout.AxesComplete || !layout.ButtonsComplete || layout.Axes.Count + layout.Buttons.Count == 0)
                throw new InvalidOperationException("Resolve incomplete or ambiguous native input evidence before adding the controller.");
            if (native.Document.HasOpaqueSyntax || native.Devices.Any(device => !device.Valid))
                throw new InvalidOperationException("Resolve unsupported syntax or invalid saved ranges before adding a controller.");
            var mappings = native.Children(native.Controls.Address, "deviceMapping");
            if (mappings.Count > 1)
                throw new InvalidOperationException("Multiple deviceMapping blocks prevent reliable controller creation.");
            var axesOffset = NextOffset(native, CommandType.Axis);
            var buttonsOffset = NextOffset(native, CommandType.Button);
            _ = checked(axesOffset + layout.Axes.Count);
            _ = checked(buttonsOffset + layout.Buttons.Count);
            var bytes = product.ToByteArray();
            var vendor = bytes[0] | bytes[1] << 8;
            var model = bytes[2] | bytes[3] << 8;
            if (vendor == 0 || model == 0)
                throw new InvalidOperationException("The connected controller must report nonzero vendor and product IDs.");
            var devId = vendor.ToString("X4", CultureInfo.InvariantCulture) + ":" + model.ToString("X4", CultureInfo.InvariantCulture);
            var escaped = name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            var text = native.Document.SerializeUnchanged();
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var declaration = string.Create(CultureInfo.InvariantCulture,
                $"joystick{{devId:t=\"{devId}\";axesOffset:i={axesOffset};axesCount:i={layout.Axes.Count};buttonsOffset:i={buttonsOffset};buttonsCount:i={layout.Buttons.Count};name:t=\"{escaped}\";}}");
            var insertion = mappings.Count == 0 ? native.Controls.End - 1 : mappings[0].End - 1;
            var addition = mappings.Count == 0 ? "deviceMapping{" + newline + declaration + newline + "}" : declaration;
            return text.Insert(insertion, newline + addition + newline);
        }

        private static int NextOffset(WarThunderNativeProfile native, CommandType type)
        {
            var devices = native.Devices.Where(device => (type == CommandType.Axis ? device.AxesCount : device.ButtonsCount) > 0)
                .OrderBy(device => type == CommandType.Axis ? device.AxesOffset : device.ButtonsOffset).ToArray();
            var end = 0;
            foreach (var device in devices)
            {
                var offset = type == CommandType.Axis ? device.AxesOffset : device.ButtonsOffset;
                if (offset < end) throw new InvalidOperationException("Overlapping saved input ranges prevent controller creation.");
                end = checked(offset + (type == CommandType.Axis ? device.AxesCount : device.ButtonsCount));
            }
            // Reserve unowned captured indices as well so adding a mapping cannot claim an old binding.
            var fieldName = type == CommandType.Axis ? "axisId" : "joyButton";
            foreach (var field in native.Bindings.SelectMany(binding => binding.Fields)
                .Where(field => WarThunderNativeProfile.Name(field.Path) == fieldName))
            {
                if (field.Type != 'i' || !int.TryParse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    throw new InvalidOperationException("Unsupported native input prevents reliable range allocation.");
                if (value >= end) end = checked(value + 1);
            }
            return end;
        }
    }
}
