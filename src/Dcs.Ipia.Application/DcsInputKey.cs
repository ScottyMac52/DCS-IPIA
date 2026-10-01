using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Matches authored hardware POV keys to the physical keys used by DCS profiles.</summary>
    public static class DcsInputKey
    {
        #region BEGIN Public
        /// <summary>Checks the known JOY_POV and JOY_BTN_POV spellings of the same physical input.</summary>
        /// <param name="first">Hardware or binding input key.</param>
        /// <param name="second">Hardware or binding input key.</param>
        /// <returns>Whether the keys identify the same input.</returns>
        public static bool SamePhysicalInput(string first, string second) => string.Equals(ToDcsKey(first),
                                                                                           ToDcsKey(second),
                                                                                           StringComparison.Ordinal);

        /// <summary>Gets a comparison key that treats known POV spellings as one assignment slot.</summary>
        /// <param name="slot">Module, device, scope, section, input, and modifier chord.</param>
        /// <returns>Physical slot identity without changing the stored input spelling.</returns>
        public static string PhysicalSlotKey(DraftAssignmentSlot slot) =>
            (slot with { InputKey = ToDcsKey(slot.InputKey) }).Key;

        /// <summary>Collapses equivalent POV aliases while retaining conflicting assignments for validation.</summary>
        /// <param name="assignments">Imported or previously saved assignments.</param>
        /// <returns>One preferred original assignment per compatible alias group.</returns>
        public static IReadOnlyList<DraftAssignment> CollapseAliases(IEnumerable<DraftAssignment> assignments) =>
            assignments.GroupBy(x => PhysicalSlotKey(x.Slot), StringComparer.Ordinal)
                .SelectMany(group => {
                    var entries = group.ToArray();
                    var first = entries[0];
                    if (entries.Select(x => x.Slot.InputKey).Distinct(StringComparer.Ordinal).Count() < 2 ||
                        entries.Any(x => x.Command.Id != first.Command.Id || x.Command.IsAxis != first.Command.IsAxis ||
                            x.Command.Layer != first.Command.Layer ||
                            System.Text.Json.JsonSerializer.Serialize(x.Filter) !=
                            System.Text.Json.JsonSerializer.Serialize(first.Filter)))
                        return entries;
                    return [entries.FirstOrDefault(x => x.Slot.InputKey == ToDcsKey(x.Slot.InputKey)) ?? first];
                }).ToArray();

        /// <summary>Uses the profile's actual DCS key when an authored hardware key has another spelling.</summary>
        /// <param name="configuration">Stored module bindings.</param>
        /// <param name="profileId">Device profile identity.</param>
        /// <param name="section">Key or axis section.</param>
        /// <param name="hardwareKey">Key from the shared hardware definition.</param>
        /// <returns>The profile key, or the DCS POV spelling for an unassigned control.</returns>
        public static string Resolve(ModuleConfiguration configuration, Guid profileId, BindingSection section,
                                     string hardwareKey, string? modifierChord = null)
        {
            if (modifierChord is not null)
            {
                var inLayer = configuration.Bindings
                    .Where(x => x.ProfileId == profileId && x.Section == section &&
                                x.Disposition == BindingDisposition.Added &&
                                SamePhysicalInput(x.InputKey, hardwareKey) &&
                                string.Join("+", x.Reformers) == modifierChord)
                    .Select(x => x.InputKey).Distinct(StringComparer.Ordinal).ToArray();
                if (inLayer.Length == 1)
                    return inLayer[0];
            }
            var actual = configuration.Bindings
                             .Where(x => x.ProfileId == profileId && x.Section == section &&
                                         SamePhysicalInput(x.InputKey, hardwareKey))
                             .Select(x => x.InputKey)
                             .Distinct(StringComparer.Ordinal)
                             .ToArray();
            return actual.Length == 1 ? actual[0] : ToDcsKey(hardwareKey);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string ToDcsKey(string key) => key.StartsWith("JOY_POV", StringComparison.Ordinal)
                                                          ? "JOY_BTN_POV" + key["JOY_POV".Length..]
                                                          : key;
        #endregion END private helpers
    }
}

