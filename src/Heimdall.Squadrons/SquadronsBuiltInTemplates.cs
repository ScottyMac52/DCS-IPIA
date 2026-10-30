namespace Heimdall.Squadrons
{
    /// <summary>Supplies version-four joystick command metadata independently of a user's assigned controls.</summary>
    internal static class SquadronsBuiltInTemplates
    {
        private static readonly IReadOnlyList<SquadronsBindingRow> Records = Load();
        #region BEGIN Public
        /// <summary>Uses captured records first; factory metadata is offered only for wholly missing joystick concepts.</summary>
        internal static IReadOnlyList<SquadronsBindingRow> Complete(IEnumerable<SquadronsBindingRow> captured)
        {
            var rows = captured.ToArray();
            var concepts = rows.Where(x => x.Type == 3).Select(x => x.Concept).ToHashSet(StringComparer.Ordinal);
            var addresses = rows.Select(x => x.RecordId).ToHashSet(StringComparer.Ordinal);
            var result = rows.ToList();
            foreach (var template in Records.Where(x => !concepts.Contains(x.Concept)))
            {
                var row = template;
                if (addresses.Contains(row.RecordId))
                {
                    // Identifier zero is the native custom-record form. Append it
                    // after occupied system addresses instead of replacing them.
                    // Fixed factory identifiers retain their exact native address.
                    if (row.Fields["identifier"] != 0) continue;
                    var index = result.Where(x => x.Concept == row.Concept).Select(x => x.Index).DefaultIfEmpty(-1).Max() + 1;
                    if (index >= 128) continue;
                    row = row with { Index = index };
                }
                if (addresses.Add(row.RecordId)) result.Add(row);
            }
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IReadOnlyList<SquadronsBindingRow> Load()
        {
            using var stream = typeof(SquadronsBuiltInTemplates).Assembly.GetManifestResourceStream("Squadrons.JoystickTemplates.v4")!;
            using var reader = new StreamReader(stream);
            return new SquadronsNativeDocument(reader.ReadToEnd()).Bindings;
        }
        #endregion END private helpers
    }
}
