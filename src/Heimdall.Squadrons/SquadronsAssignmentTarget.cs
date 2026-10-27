namespace Heimdall.Squadrons
{
    /// <summary>Resolves an existing native record for a drawing input without confusing
    /// positive and negative axis declarations with separate physical controls.</summary>
    public static class SquadronsAssignmentTarget
    {
        /// <summary>Returns the unique matching captured record or null when creation is needed.
        /// An ambiguous match never silently overwrites another controller's binding.</summary>
        public static SquadronsBindingRow? Find(IEnumerable<SquadronsBindingRow> records,
            IReadOnlyCollection<string> editableIds, SquadronsInput input, int nativeDeviceId)
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentNullException.ThrowIfNull(editableIds);
            ArgumentNullException.ThrowIfNull(input);
            var eligible = records.Where(row => editableIds.Contains(row.RecordId) &&
                row.Input?.Type == input.Type &&
                (input.Type != Heimdall.Domain.MultiSim.CommandType.Axis ||
                 row.Input.Direction == input.Direction)).ToArray();
            if (eligible.Length == 1) return eligible[0];
            if (eligible.Length == 0) return null;
            var local = eligible.Where(row => row.DeviceId == nativeDeviceId).ToArray();
            if (local.Length == 1) return local[0];
            throw new InvalidOperationException(
                "Multiple native records match this input. Select a specific saved assignment to replace.");
        }
    }
}
