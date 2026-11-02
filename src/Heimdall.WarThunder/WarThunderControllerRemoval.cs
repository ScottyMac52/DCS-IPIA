using System.Globalization;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Removes explicit native dependencies without changing surviving global input indices or tuning.</summary>
    internal sealed record WarThunderControllerRemoval(string Text, IReadOnlyDictionary<string, string> RemainingIds, string Summary)
    {
        private static readonly CommandType[] InputTypes = [CommandType.Axis, CommandType.Button];
        #region BEGIN Public
        internal static WarThunderControllerRemoval Create(WarThunderNativeProfile native, string logicalId)
        {
            var device = native.Devices.SingleOrDefault(item => item.Id == logicalId)
                ?? throw new InvalidOperationException("Select a saved controller to remove.");
            if (native.Devices.Any(item => !item.Valid) || native.Document.HasOpaqueSyntax)
                throw new InvalidOperationException("Resolve unsupported syntax or invalid saved ranges before removing a controller.");
            RequireSeparateRanges(native.Devices);
            var bindings = DependentBindings(native, device);
            var text = native.Document.SerializeUnchanged();
            var spans = bindings.Select(binding => native.Document.Blocks.Single(block => block.Address == binding.Id))
                .Select(block => (Start: block.Start + 1, End: block.End - 1)).ToList();
            var mapping = native.Document.Blocks.Single(block => block.Address == device.RecordId);
            spans.Add((mapping.HeaderStart, mapping.End));
            foreach (var span in spans.OrderByDescending(span => span.Start)) text = text[..span.Start] + text[span.End..];
            var remaining = native.Devices.Where(item => item.Id != logicalId).Select((item, index) =>
                (item.Id, Next: "war-thunder.joystick." + index.ToString(CultureInfo.InvariantCulture)))
                .ToDictionary(item => item.Id, item => item.Next, StringComparer.Ordinal);
            return new(text, remaining, "Remove controller: " + device.Name + " · " + logicalId
                + "\nClear " + bindings.Count.ToString(CultureInfo.InvariantCulture) + " dependent native alternative(s), including complete chords or mixed alternatives."
                + "\nSurviving controllers retain their native offsets and assignments. Physical and drawing links follow their original units."
                + "\nSave the profile and export a review copy. Discard restores the saved configuration.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static List<WarThunderBinding> DependentBindings(WarThunderNativeProfile native, WarThunderDevice device)
        {
            var result = new List<WarThunderBinding>();
            foreach (var binding in native.Bindings)
            {
                var dependent = binding.Fields.Any(field => IsOwnedInput(field, device));
                if (binding.HasChildren) throw new InvalidOperationException("Nested native command data blocks controller removal; its dependencies cannot be verified.");
                if (dependent) result.Add(binding);
            }
            return result;
        }
        private static bool IsOwnedInput(WarThunderBlkScalar field, WarThunderDevice device)
        {
            var name = WarThunderNativeProfile.Name(field.Path);
            if (name is not ("joyButton" or "axisId")) return false;
            if (field.Type != 'i' || !int.TryParse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException("Unsupported native input prevents reliable controller removal.");
            return device.Contains(name == "axisId" ? CommandType.Axis : CommandType.Button, value);
        }
        private static void RequireSeparateRanges(IReadOnlyList<WarThunderDevice> devices)
        {
            foreach (var type in InputTypes)
            {
                var sorted = devices.Where(device => (type == CommandType.Axis ? device.AxesCount : device.ButtonsCount) > 0)
                    .OrderBy(device => type == CommandType.Axis ? device.AxesOffset : device.ButtonsOffset).ToArray();
                for (var index = 1; index < sorted.Length; index++)
                {
                    var previous = sorted[index - 1]; var current = sorted[index];
                    var end = type == CommandType.Axis ? (long)previous.AxesOffset + previous.AxesCount : (long)previous.ButtonsOffset + previous.ButtonsCount;
                    var start = type == CommandType.Axis ? current.AxesOffset : current.ButtonsOffset;
                    if (start < end) throw new InvalidOperationException("Overlapping native ranges block controller removal.");
                }
            }
        }
        #endregion END private helpers
    }
}
