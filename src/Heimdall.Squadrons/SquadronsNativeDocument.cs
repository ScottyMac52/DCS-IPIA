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
        /// <summary>Appends a complete version-four joystick record using proven identifiers from
        /// existing records for the exact command concept. Unknown command templates fail closed.</summary>
        public string AppendJoystickRecord(string concept, int slot, SquadronsInput input)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(concept);
            ArgumentNullException.ThrowIfNull(input);
            if (slot is < 1 or > 10 || !Values.ContainsKey("GstInput.JoystickDevice" + slot))
                throw new InvalidOperationException("Select an existing named Squadrons joystick slot.");
            var templates = Bindings.Where(row => row.Concept == concept &&
                row.Fields.Count == RequiredFields.Length && row.Fields["modifier"] == 0 &&
                row.Fields["altbutton"] == 0).ToArray();
            var identifiers = templates.Select(x => x.Fields["identifier"]).Distinct().ToArray();
            if (identifiers.Length != 1)
                throw new InvalidOperationException("A native record template for this command is not verified. Capture a binding for this command once to establish its identifier.");
            if (Bindings.Count(x => x.Concept == concept) >= 128)
                throw new InvalidOperationException("Too many native binding records for this command.");
            // Preserve existing concept-local slot ordering instead of filling an
            // earlier hole (which may represent a different input family).
            // The game's acceptance of appended slots still requires in-game verification.
            var index = Bindings.Where(x => x.Concept == concept).Max(x => x.Index) + 1;
            if (index >= 128) throw new InvalidOperationException("No free native binding index.");
            var encoded = SquadronsInputCodec.Encode(input);
            var values = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["altbutton"] = 0, ["axis"] = encoded.Axis, ["button"] = encoded.Button,
                ["deviceid"] = slot - 1, ["identifier"] = identifiers[0], ["modifier"] = 0,
                ["negate"] = 0, ["type"] = 3
            };
            var newline = Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var prefix = BindingPrefix + concept + "." + index + ".";
            var lines = RequiredFields.Select(field => prefix + field + " " +
                values[field].ToString(CultureInfo.InvariantCulture));
            var separator = Text.EndsWith("\n", StringComparison.Ordinal) ? "" : newline;
            return Text + separator + string.Join(newline, lines) + newline;
        }
        /// <summary>Removes exactly one scalar setting without changing unrelated native lines.</summary>
        public string RemoveSetting(string key)
        {
            if (!Values.ContainsKey(key)) throw new InvalidOperationException("Setting does not exist.");
            var lines = Text.Split('\n');
            var kept = lines.Where(line => !line.StartsWith(key + " ", StringComparison.Ordinal)).ToArray();
            if (kept.Length != lines.Length - 1) throw new InvalidOperationException("Ambiguous setting removal.");
            return string.Join("\n", kept);
        }
        /// <summary>Removes one exact native joystick record, never an unrelated command or system input.</summary>
        public string RemoveJoystickRecord(string recordId)
        {
            var selected = Bindings.SingleOrDefault(row => row.RecordId == recordId);
            if (selected is null || selected.Type != 3)
                throw new InvalidOperationException("Select one existing joystick binding record.");
            var prefix = BindingPrefix + recordId + ".";
            var lines = Text.Split('\n');
            var retained = lines.Where(line => !line.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (lines.Length - retained.Length != selected.Fields.Count)
                throw new InvalidOperationException("Native record removal did not match the complete captured record.");
            return string.Join("\n", retained);
        }
        /// <summary>Deletes complete indexed joystick binding records for one captured slot, including unsupported encodings.</summary>
        public string RemoveJoystickBindings(int slot)
        {
            if (slot is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(slot));
            var prefixes = Bindings.Where(row => row.Type == 3 && row.DeviceId == slot - 1)
                .Select(row => BindingPrefix + row.RecordId + ".").ToArray();
            return string.Join("\n", Text.Split('\n').Where(line =>
                !prefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))));
        }
        /// <summary>Atomically reassigns native declarations and all joystick device references to a contiguous ordered slot list.</summary>
        public string ReorderJoystickSlots(IReadOnlyList<int> orderedSlots)
        {
            ArgumentNullException.ThrowIfNull(orderedSlots);
            if (orderedSlots.Count is < 1 or > 10 || orderedSlots.Distinct().Count() != orderedSlots.Count
                || orderedSlots.Any(slot => slot is < 1 or > 10
                    || !Values.ContainsKey("GstInput.JoystickDevice" + slot)))
                throw new InvalidOperationException("Supply each captured joystick slot exactly once.");
            var declared = Values.Keys.Where(x => x.StartsWith("GstInput.JoystickDevice", StringComparison.Ordinal)).ToArray();
            if (declared.Length != orderedSlots.Count)
                throw new InvalidOperationException("Reordering must include every captured joystick slot.");
            var map = orderedSlots.Select((slot, index) => (slot, next: index + 1)).ToDictionary(x => x.slot, x => x.next);
            if (Bindings.Any(row => row.Type == 3 && !map.ContainsKey(row.DeviceId + 1)))
                throw new InvalidOperationException("Native joystick bindings reference a slot absent from this profile.");
            var deviceKeys = Values.Keys.Where(x => x.StartsWith("GstInput.JoystickDevice", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            var bindingKeys = Bindings.Where(x => x.Type == 3).Select(x => BindingPrefix + x.RecordId + ".deviceid").ToDictionary(
                key => key, key => map[int.Parse(Values[key], CultureInfo.InvariantCulture) + 1] - 1, StringComparer.Ordinal);
            return string.Join("\n", Text.Split('\n').Select(line =>
            {
                var delimiter = line.IndexOf(' ');
                if (delimiter < 0) return line;
                var key = line[..delimiter];
                if (deviceKeys.Contains(key))
                {
                    var slot = int.Parse(key["GstInput.JoystickDevice".Length..], CultureInfo.InvariantCulture);
                    return "GstInput.JoystickDevice" + map[slot] + line[delimiter..];
                }
                if (bindingKeys.TryGetValue(key, out var deviceId))
                    return key + " " + deviceId.ToString(CultureInfo.InvariantCulture) + (line.EndsWith('\r') ? "\r" : "");
                return line;
            }));
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
