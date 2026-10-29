using System.Globalization;

namespace Heimdall.Squadrons
{
    /// <summary>Preserves captured joystick record metadata separately from active exported assignments.</summary>
    internal static class SquadronsTemplateArchive
    {
        #region BEGIN Public
        /// <summary>Reads only native joystick records; archive settings cannot alter exported configuration.</summary>
        internal static IReadOnlyList<SquadronsBindingRow> Read(string? text)
        {
            if (text is null) return [];
            var document = new SquadronsNativeDocument(text);
            if (document.Bindings.Any(x => x.Type != 3) ||
                document.Values.Count != 1 + document.Bindings.Sum(x => x.Fields.Count))
                throw new InvalidOperationException("Invalid Squadrons assignment template archive.");
            return document.Bindings;
        }
        /// <summary>Retains the most recently captured fields for each exact native address.</summary>
        internal static string Capture(string? previous, IEnumerable<SquadronsBindingRow> records)
        {
            var templates = Read(previous).ToDictionary(x => x.RecordId, StringComparer.Ordinal);
            foreach (var row in records.Where(x => x.Type == 3)) templates[row.RecordId] = row;
            return "GstKeyBinding.InputDataVersion 4\n" + string.Join("", templates.Values
                .OrderBy(x => x.RecordId, StringComparer.Ordinal).Select(Serialize));
        }
        /// <summary>Restores only requested missing addresses; existing records are never overwritten.</summary>
        internal static SquadronsNativeDocument Restore(SquadronsNativeDocument document, string? archive, IReadOnlyCollection<string> ids)
        {
            var present = document.Bindings.Select(x => x.RecordId).ToHashSet(StringComparer.Ordinal);
            var missing = Read(archive).Where(x => ids.Contains(x.RecordId) && !present.Contains(x.RecordId)).ToArray();
            if (missing.Length == 0) return document;
            var separator = document.Text.EndsWith('\n') ? "" : "\n";
            var version = document.Values.ContainsKey("GstKeyBinding.InputDataVersion") ? "" : "GstKeyBinding.InputDataVersion 4\n";
            return new(document.Text + separator + version + string.Join("", missing.Select(Serialize)));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Serialize(SquadronsBindingRow row) => string.Join("", row.Fields.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(field => "GstKeyBinding." + row.RecordId + "." + field.Key + " " + field.Value.ToString(CultureInfo.InvariantCulture) + "\n"));
        #endregion END private helpers
    }
}
