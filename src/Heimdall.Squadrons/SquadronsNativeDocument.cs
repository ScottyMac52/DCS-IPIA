using System.Globalization;

namespace Heimdall.Squadrons
{
    /// <summary>Reads bounded native settings while retaining exact source text and unknown scalar fields.</summary>
    internal sealed class SquadronsNativeDocument
    {
        private static readonly string[] RequiredFields = ["altbutton", "axis", "button", "deviceid", "identifier", "modifier", "negate", "type"];
        private const string BindingPrefix = "GstKeyBinding.";
        #region BEGIN Public
        /// <summary>Gets the original source without normalizing whitespace or line endings.</summary>
        public string Text { get; }
        /// <summary>Gets exact setting values.</summary>
        public IReadOnlyDictionary<string, string> Values { get; }
        /// <summary>Gets every captured record, including records not projected into portable assignments.</summary>
        public IReadOnlyList<SquadronsBindingRow> Bindings { get; }
        /// <summary>Validates native version four settings before projecting or editing them.</summary>
        public SquadronsNativeDocument(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4 * 1024 * 1024 || text.Contains('\0'))
                throw new InvalidOperationException("Squadrons settings must be nonempty text below 4 MiB.");
            Text = text;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in text.Split('\n'))
            {
                var clean = line.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(clean)) continue;
                var split = clean.IndexOf(' ');
                if (split <= 0 || !values.TryAdd(clean[..split], clean[(split + 1)..]))
                    throw new InvalidOperationException("Malformed or duplicate Squadrons setting.");
            }
            if (values.GetValueOrDefault("GstKeyBinding.InputDataVersion") != "4")
                throw new InvalidOperationException("Only captured Squadrons input data version 4 is supported.");
            Values = values; Bindings = ReadBindings(values);
        }
        /// <summary>Changes existing scalar values only, retaining unrelated lines and their original endings.</summary>
        public string ReplaceValues(IReadOnlyDictionary<string, int> changes)
        {
            var lines = Text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var split = lines[index].IndexOf(' ');
                if (split > 0 && changes.TryGetValue(lines[index][..split], out var value))
                    lines[index] = lines[index][..(split + 1)] + value.ToString(CultureInfo.InvariantCulture)
                        + (lines[index].EndsWith('\r') ? "\r" : "");
            }
            return string.Join("\n", lines);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static List<SquadronsBindingRow> ReadBindings(Dictionary<string, string> values)
        {
            var records = new Dictionary<(string Concept, int Index), Dictionary<string, int>>();
            foreach (var (key, value) in values.Where(x => x.Key.StartsWith(BindingPrefix, StringComparison.Ordinal) && x.Key != "GstKeyBinding.InputDataVersion"))
            {
                var address = key[BindingPrefix.Length..]; var fieldSplit = address.LastIndexOf('.');
                var record = fieldSplit > 0 ? address[..fieldSplit] : ""; var indexSplit = record.LastIndexOf('.');
                if (indexSplit <= 0 || !int.TryParse(record[(indexSplit + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    throw new InvalidOperationException("Malformed Squadrons binding address or numeric field.");
                var id = (record[..indexSplit], index);
                if (!records.TryGetValue(id, out var fields)) records.Add(id, fields = new(StringComparer.Ordinal));
                fields.Add(address[(fieldSplit + 1)..], number);
            }
            if (records.Values.Any(fields => RequiredFields.Any(field => !fields.ContainsKey(field))))
                throw new InvalidOperationException("Incomplete Squadrons native binding record.");
            return records.Select(x => new SquadronsBindingRow(x.Key.Concept, x.Key.Index, x.Value)).ToList();
        }
        #endregion END private helpers
    }
}
