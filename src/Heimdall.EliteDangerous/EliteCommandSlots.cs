namespace Heimdall.EliteDangerous
{
    /// <summary>Explains command-wide native slot ownership independently of the selected controller.</summary>
    public static class EliteCommandSlots
    {
        #region BEGIN Public
        /// <summary>Shows every native slot, including bindings owned by other devices and empty slots.</summary>
        public static string Summary(IEnumerable<EliteAssignmentRow> rows, string commandId, bool axis,
            IReadOnlyDictionary<string, string> devices)
        {
            var assignments = rows.Where(x => x.CommandId == commandId).ToArray();
            var slots = axis ? new[] { "Binding" } : new[] { "Primary", "Secondary" };
            var rule = axis ? "One axis binding per command." : "Two bindings per command across all devices; either activates the command.";
            return rule + "\n" + string.Join("\n", slots.Select(slot =>
            {
                var row = assignments.SingleOrDefault(x => x.Slot == slot);
                return slot + ": " + (row is null ? "Unassigned" : Describe(row, devices));
            }));
        }
        /// <summary>Makes replacing an occupied slot explicit before the draft is applied.</summary>
        public static string Replacement(IEnumerable<EliteAssignmentRow> rows, string commandId, string slot,
            IReadOnlyDictionary<string, string> devices)
        {
            var row = rows.SingleOrDefault(x => x.CommandId == commandId && x.Slot == slot);
            return row is null ? "Assignment changes ready to apply."
                : "Applying replaces " + slot + " (" + Describe(row, devices) + "). Other slots are preserved.";
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Describe(EliteAssignmentRow row, IReadOnlyDictionary<string, string> devices) =>
            devices.GetValueOrDefault(row.DeviceId, row.DeviceId) + " · " + row.Input
                + (row.Modifiers.Count == 0 ? "" : " + " + string.Join(" + ", row.Modifiers.Select(x =>
                    devices.GetValueOrDefault(x.Source, x.Source) + ": " + x.Value)))
                + (row.Mode == Heimdall.Domain.MultiSim.BindingMode.Hold ? " [Hold]" : "");
        #endregion END private helpers
    }
}
