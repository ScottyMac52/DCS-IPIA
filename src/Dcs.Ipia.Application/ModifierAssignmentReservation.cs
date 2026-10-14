namespace Dcs.Ipia.Application
{
    /// <summary>Reserves physical inputs used as modifiers in either workspace scope.</summary>
    public static class ModifierAssignmentReservation
    {
        #region BEGIN Public
        /// <summary>Finds modifiers that reserve a physical button, including aliases and shared device membership.</summary>
        /// <param name="state">Current staged device and modifier management state.</param>
        /// <param name="profileId">Selected assignment device profile.</param>
        /// <param name="inputKey">Physical button or POV key.</param>
        /// <returns>All definitions reserving that input, in consistent scope/name order.</returns>
        public static IReadOnlyList<WorkspaceModifier> Find(WorkspaceManagement state, Guid profileId, string inputKey)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(inputKey);
            return state.Modifiers.Where(modifier => MatchesDevice(state, profileId, modifier) &&
                    DcsInputKey.SamePhysicalInput(modifier.Key, inputKey))
                .OrderBy(modifier => modifier.Layer).ThenBy(modifier => modifier.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Explains which modifier must be changed or removed before adding an assignment.</summary>
        public static string BlockReason(string inputKey, IReadOnlyList<WorkspaceModifier> reserved)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputKey);
            ArgumentNullException.ThrowIfNull(reserved);
            return inputKey + " is reserved as " +
                string.Join(", ", reserved.Select(Description)) +
                ". Change or remove the modifier in Modifiers before assigning a command.";
        }

        /// <summary>Provides a readable scope/name/mode label for modifier callouts and tooltips.</summary>
        public static string Description(WorkspaceModifier modifier) =>
            (modifier.Layer == AssignmentLayer.UiLayer ? "UI Layer" : "Module") +
            " modifier " + modifier.Name + " (" + modifier.Mode + ")";
        #endregion END Public

        #region BEGIN private helpers
        private static bool MatchesDevice(WorkspaceManagement state, Guid profileId, WorkspaceModifier modifier)
        {
            if (profileId == modifier.ProfileId && profileId != Guid.Empty) return true;
            var selected = state.Devices.Where(device => device.ProfileId == profileId).ToArray();
            if (selected.Length == 0) return false;
            if (modifier.ProfileId != Guid.Empty)
            {
                var owners = state.Devices.Where(device => device.ProfileId == modifier.ProfileId).ToArray();
                return selected.Any(a => a.PhysicalGuid is { } guid &&
                    owners.Any(b => b.PhysicalGuid == guid));
            }
            // Legacy unresolved modifiers: reserve only when the imported native device name is unambiguous.
            var matching = state.Devices.Where(device => Native(device.FileName).Equals(
                Native(modifier.NativeDevice), StringComparison.OrdinalIgnoreCase))
                .Select(device => device.ProfileId).Distinct().ToArray();
            return matching.Length == 1 && matching[0] == profileId;
        }

        private static string Native(string file) =>
            file.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase) ? file[..^9] : file;
        #endregion END private helpers
    }
}
