using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>Rejects changes that cannot be represented faithfully by this adapter version.</summary>
    internal static class EliteProfileValidation
    {
        #region BEGIN Public
        /// <summary>Validates envelope and captured device membership before preparing output.</summary>
        public static void Validate(ProfileDocument profile, ProfileDocument baseline)
        {
            if (profile.ProfileId != baseline.ProfileId || profile.Name != baseline.Name || profile.Devices is null
                || profile.Metadata is null || profile.Devices.Count != baseline.Devices.Count
                || profile.Devices.Any(x => x is null))
            {
                throw new InvalidOperationException("Preset identity and captured device membership must remain unchanged.");
            }
            ValidateDevices(profile, baseline);
            if (!profile.Metadata.Keys.Order(StringComparer.Ordinal).SequenceEqual(baseline.Metadata.Keys.Order(StringComparer.Ordinal))
                || profile.Metadata.Values.Any(x => x is not "0" and not "1"))
            {
                throw new InvalidOperationException("Only existing Elite command toggle flags may be changed in metadata.");
            }
        }

        /// <summary>Reads the explicit assignment slot after JSON round trips.</summary>
        public static string Slot(Dictionary<string, object> extensions)
        {
            if (extensions is null || extensions.Count != 1 || !extensions.TryGetValue("ed.slot", out var value))
            {
                throw new InvalidOperationException("Every Elite assignment needs its native ed.slot identity.");
            }
            return value?.ToString() ?? throw new InvalidOperationException("Empty Elite assignment slot.");
        }

        /// <summary>Validates that an axis edit has a supported native meaning.</summary>
        public static void Axis(AxisBinding binding)
        {
            if (binding.Curve != CurveType.Linear || binding.Sensitivity != 1 || !float.IsFinite(binding.Deadzone)
                || binding.Deadzone < 0 || binding.Deadzone > 100 || Slot(binding.SimSpecific) != "Binding")
            {
                throw new InvalidOperationException("Elite axes support inversion and 0–100% deadzone; curve and sensitivity conversion is unavailable.");
            }
        }

        /// <summary>Validates supported per-binding modes without confusing command toggle flags.</summary>
        public static void Button(ButtonBinding binding)
        {
            if (binding.Mode is not BindingMode.Press and not BindingMode.Hold || Slot(binding.SimSpecific) is not "Primary" and not "Secondary")
            {
                throw new InvalidOperationException("Elite buttons support Primary/Secondary press or hold; edit command toggles separately.");
            }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void ValidateDevices(ProfileDocument profile, ProfileDocument baseline)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var device in profile.Devices)
            {
                var original = baseline.Devices.Find(x => x.LogicalId == device.LogicalId);
                if (!seen.Add(device.LogicalId) || original is null || device.ContextId != baseline.ContextId
                    || !EliteProfileProjection.Equal(device.Identity, original.Identity) || device.Buttons is null || device.Axes is null
                    || device.Buttons.Any(x => x is null) || device.Axes.Any(x => x is null))
                {
                    throw new InvalidOperationException("Elite device identity or context was changed.");
                }
            }
        }
        #endregion END private helpers
    }
}
