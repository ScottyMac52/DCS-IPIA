namespace Dcs.Ipia.Application
{
    /// <summary>Filters a command palette using staged assignments across module device profiles.</summary>
    public static class CommandBindingStateFilter
    {
        #region BEGIN Public
        /// <summary>Applies local or module-wide binding state to already filtered commands.</summary>
        /// <param name="commands">Palette commands in the selected scope.</param>
        /// <param name="assignments">Current draft assignments.</param>
        /// <param name="layer">Module controls or module UI Layer.</param>
        /// <param name="eligibleProfiles">Device profiles available in the selected scope.</param>
        /// <param name="selectedProfile">Selected device profile, if any.</param>
        /// <param name="selectedChord">Selected device modifier chord; empty means Base.</param>
        /// <param name="state">Desired local or module-wide state.</param>
        /// <returns>Commands whose assignments match the selected state.</returns>
        public static IReadOnlyList<CommandPaletteEntry> Filter(
            IEnumerable<CommandPaletteEntry> commands, IEnumerable<DraftAssignment> assignments, AssignmentLayer layer,
            IEnumerable<Guid> eligibleProfiles, Guid? selectedProfile, string selectedChord, CommandBindingState state)
        {
            ArgumentNullException.ThrowIfNull(commands);
            ArgumentNullException.ThrowIfNull(assignments);
            ArgumentNullException.ThrowIfNull(eligibleProfiles);
            ArgumentNullException.ThrowIfNull(selectedChord);
            if (!Enum.IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state));
            var profiles = eligibleProfiles.Distinct().ToArray();
            var eligible = profiles.ToHashSet();
            var scoped = assignments.Where(x => x.Slot.Layer == layer && eligible.Contains(x.Slot.ProfileId)).ToArray();
            var local = scoped.Where(x => x.Slot.ProfileId == selectedProfile && x.Slot.ModifierChord == selectedChord)
                            .Select(x => x.Command.Id)
                            .ToHashSet(StringComparer.Ordinal);
            var byProfile =
                scoped.GroupBy(x => x.Slot.ProfileId)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.Command.Id).ToHashSet(StringComparer.Ordinal));
            return commands
                .Where(command => state switch {
                    CommandBindingState.All => true, CommandBindingState.Bound => local.Contains(command.Id),
                    CommandBindingState.Unbound => !local.Contains(command.Id),
                    CommandBindingState.BoundByAll =>
                        profiles.Length > 0 && profiles.All(profile => byProfile.TryGetValue(profile, out var ids) &&
                                                                       ids.Contains(command.Id)),
                    CommandBindingState.UnboundByAll => profiles.All(
                        profile => !byProfile.TryGetValue(profile, out var ids) || !ids.Contains(command.Id)),
                    _ => false
                })
                .ToArray();
        }
        #endregion END Public
    }
}
