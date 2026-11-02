using System.Globalization;

namespace Heimdall.WarThunder
{
    /// <summary>Describes editable native record shapes independently of profile loading and mutation.</summary>
    internal static class WarThunderRecordCapabilities
    {
        private static readonly HashSet<string> AxisParameters = new(StringComparer.Ordinal)
            { "inverse", "kMul", "innerDeadzone", "nonlinearity", "relative" };
        #region BEGIN Public
        internal static bool IsAxisParameter(string name) => AxisParameters.Contains(name);
        internal static WarThunderBindingCapabilities Create(WarThunderNativeProfile native, WarThunderBinding binding)
        {
            if (native.Document.HasOpaqueSyntax || binding.HasChildren || RepeatedAxis(native, binding))
                return new(false, false, false, false, []);
            var validRanges = native.Devices.Count > 0 && native.Devices.All(device => device.Valid);
            var canAdd = validRanges && !native.Bindings.Any(other => other.Section == binding.Section
                && other.Command == binding.Command && other.HasChildren);
            return binding.Section == "axes" ? Axis(binding, validRanges, canAdd) : Hotkey(binding, validRanges, canAdd);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool RepeatedAxis(WarThunderNativeProfile native, WarThunderBinding binding) => binding.Section == "axes"
            && native.Bindings.Count(other => other.Section == binding.Section && other.Command == binding.Command) != 1;
        private static WarThunderBindingCapabilities Axis(WarThunderBinding binding, bool validRanges, bool canAdd)
        {
            var fields = binding.Fields.Where(field => WarThunderNativeProfile.Name(field.Path) == "axisId").ToArray();
            var assigned = fields.Count(field => field.Type == 'i') == 1;
            var flags = binding.Fields.Where(field => WarThunderNativeProfile.Name(field.Path) == "inverse").ToArray();
            return new(assigned && validRanges, ClearAxis(fields), canAdd && fields.Length == 0,
                assigned && flags.Length <= 1 && flags.All(field => field.Type == 'b'), Parameters(binding));
        }
        private static bool ClearAxis(WarThunderBlkScalar[] fields) => fields.Length == 1 && fields[0].Type == 'i'
            && int.TryParse(fields[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0;
        private static string[] Parameters(WarThunderBinding binding) => binding.Fields
            .Where(field => IsAxisParameter(WarThunderNativeProfile.Name(field.Path)))
            .GroupBy(field => field.Path, StringComparer.Ordinal).Where(group => group.Count() == 1)
            .Select(group => group.Single().Address).ToArray();
        private static WarThunderBindingCapabilities Hotkey(WarThunderBinding binding, bool validRanges, bool canAdd)
        {
            var assigned = binding.Fields.Any(field => WarThunderNativeProfile.Name(field.Path) == "joyButton" && field.Type == 'i');
            var clear = assigned && binding.Fields.All(field => WarThunderNativeProfile.Name(field.Path) is "joyButton" or "activationType");
            return new(assigned && validRanges, clear, canAdd, false, []);
        }
        #endregion END private helpers
    }
}
