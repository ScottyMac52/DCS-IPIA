using System.Text.Json;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Projects native records for independent file-backed range rules; it does not encode or repair bindings.</summary>
    internal static class WarThunderValidationSnapshot
    {
        #region BEGIN Public
        internal static JsonElement Create(ProfileDocument profile)
        {
            var inspection = WarThunderProfileBackend.Inspect(profile);
            var bindings = WarThunderProfileBackend.ReadBindings(profile);
            return JsonSerializer.SerializeToElement(new
            {
                controlsVersion = inspection.GetProperty("controlsVersion").GetInt32(),
                opaqueSyntax = inspection.GetProperty("opaqueSyntax").GetBoolean(),
                devices = WarThunderProfileBackend.ReadDevices(profile).Select(device => new
                { id = device.Id, axesOffset = device.AxesOffset, axesCount = device.AxesCount, buttonsOffset = device.ButtonsOffset, buttonsCount = device.ButtonsCount }),
                axisInputs = Inputs(bindings, "axisId"), buttonInputs = Inputs(bindings, "joyButton"),
                commands = bindings.GroupBy(binding => binding.Section + "/" + binding.Command, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Select(binding => new
                    {
                        id = binding.Id,
                        parameters = binding.Fields.GroupBy(WarThunderWorkspaceProjection.FieldName, StringComparer.Ordinal)
                            .ToDictionary(fields => fields.Key, fields => fields.Select(scalar => new { type = scalar.Type.ToString(), value = Value(scalar), raw = scalar.Value }).ToArray(), StringComparer.Ordinal)
                    }).ToArray(), StringComparer.Ordinal)
            });
        }
        #endregion END Public
        #region BEGIN private helpers
        private static object Value(WarThunderBlkScalar scalar)
        {
            if (scalar.Type == 'i' && int.TryParse(scalar.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer)) return integer;
            if (scalar.Type == 'r' && double.TryParse(scalar.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var real) && double.IsFinite(real)) return real;
            if (scalar.Type == 'b' && scalar.Value is "yes" or "1") return true;
            if (scalar.Type == 'b' && scalar.Value is "no" or "0") return false;
            return scalar.Type is 's' or 't' ? WarThunderNativeProfile.Text(scalar.Value) : scalar.Value;
        }
        private static IEnumerable<Dictionary<string, object?>> Inputs(IReadOnlyList<WarThunderBinding> bindings, string name) =>
            bindings.SelectMany(binding => binding.Fields.Where(scalar => WarThunderWorkspaceProjection.FieldName(scalar) == name)
                .Select(scalar => new Dictionary<string, object?>
                {
                    ["id"] = binding.Id + "/" + scalar.Address,
                    ["index"] = scalar.Type == 'i' && int.TryParse(scalar.Value, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var number) ? (object)number : scalar.Value
                }));
        #endregion END private helpers
    }
}
