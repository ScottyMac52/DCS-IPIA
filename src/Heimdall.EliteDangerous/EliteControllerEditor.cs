using System.Text.Json;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Transforms native controller assignments without dropping modifier requirements from a surviving chord.</summary>
    internal static class EliteControllerEditor
    {
        #region BEGIN Public
        /// <summary>Clears owned assignments and dependent chords, then removes one non-system controller.</summary>
        internal static ProfileDocument Remove(ProfileDocument profile, string logicalId)
        {
            var result = Clone(profile);
            var source = Controller(result, logicalId);
            foreach (var device in result.Devices)
            {
                device.Buttons.RemoveAll(x => x.Modifiers.Any(m => m.Source == logicalId));
                device.Axes.RemoveAll(x => x.Modifiers.Any(m => m.Source == logicalId));
            }
            result.Devices.Remove(source);
            return result;
        }
        /// <summary>Moves supported inputs and modifiers; clears whole bindings when the replacement lacks a required input.</summary>
        internal static ProfileDocument Replace(ProfileDocument profile, string logicalId, string targetId, IReadOnlyCollection<string> nativeInputs)
        {
            ArgumentNullException.ThrowIfNull(nativeInputs);
            if (nativeInputs.Count == 0 || nativeInputs.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Replacement requires verified native input capabilities.");
            var result = Clone(profile);
            var source = Controller(result, logicalId);
            var target = Controller(result, targetId);
            var keys = nativeInputs.ToHashSet(StringComparer.Ordinal);
            foreach (var device in result.Devices)
            {
                device.Buttons.RemoveAll(x => (device == source && !keys.Contains(x.SourceButton))
                    || x.Modifiers.Any(m => m.Source == logicalId && !keys.Contains(m.Value!)));
                device.Axes.RemoveAll(x => (device == source && !keys.Contains(x.SourceAxis))
                    || x.Modifiers.Any(m => m.Source == logicalId && !keys.Contains(m.Value!)));
                foreach (var modifier in device.Buttons.SelectMany(x => x.Modifiers).Concat(device.Axes.SelectMany(x => x.Modifiers)))
                    if (modifier.Source == logicalId) modifier.Source = targetId;
            }
            if (source != target)
            {
                target.Buttons.AddRange(source.Buttons);
                target.Axes.AddRange(source.Axes);
                result.Devices.Remove(source);
            }
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ProfileDocument Clone(ProfileDocument profile) => JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;
        private static LogicalDevice Controller(ProfileDocument profile, string id)
        {
            var device = profile.Devices.SingleOrDefault(x => x.LogicalId == id)
                ?? throw new InvalidOperationException("Choose a controller in this profile.");
            if (device.Identity!.SimSpecificIds["ed.device"] is "Keyboard" or "Mouse")
                throw new InvalidOperationException("Keyboard and mouse system references cannot be removed or replaced as controllers.");
            return device;
        }
        #endregion END private helpers
    }
}
