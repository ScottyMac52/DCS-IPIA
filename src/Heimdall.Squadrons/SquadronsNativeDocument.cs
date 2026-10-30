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
        /// <summary>Validates native version four settings or a fresh input configuration with no binding table.</summary>
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
            if (values.GetValueOrDefault("GstKeyBinding.InputDataVersion") != "4" &&
                !(values.GetValueOrDefault("GstKeyBinding.InputDataVersion") is null &&
                  values.Keys.Any(x => x.StartsWith("GstInput.", StringComparison.Ordinal)) &&
                  !values.Keys.Any(x => x.StartsWith(BindingPrefix, StringComparison.Ordinal))))
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
                throw new InvalidOperationException("This profile has no unambiguous native identifier template for the selected command. Creating a record without one could corrupt the exported controls; creation remains unavailable for this command.");
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
            var separator = Text.EndsWith('\n') ? "" : newline;
            return Text + separator + string.Join(newline, lines) + newline;
        }
        /// <summary>Creates a named declaration in the first blank or absent unreferenced slot.</summary>
        public string AddJoystickDevice(string name)
        {
            ValidateJoystickName(name, null);
            const string prefix = "GstInput.JoystickDevice";
            var slot = Enumerable.Range(1, 10).FirstOrDefault(number =>
                string.IsNullOrWhiteSpace(Values.GetValueOrDefault(prefix + number)) &&
                !Bindings.Any(row => row.Type == 3 && row.DeviceId == number - 1));
            if (slot == 0) throw new InvalidOperationException("No unused Squadrons joystick slot is available in the supported 1–10 range. Remove an unused controller first.");
            return GroupJoystickDevices(new Dictionary<string, string> { [prefix + slot] = name });
        }
        /// <summary>Changes one existing declaration and groups device names in the game-generated settings block.</summary>
        public string RenameJoystickDevice(int slot, string name)
        {
            var key = "GstInput.JoystickDevice" + slot;
            if (!Values.TryGetValue(key, out var previous) || string.IsNullOrWhiteSpace(previous))
                throw new InvalidOperationException("Choose an existing named Squadrons controller slot.");
            ValidateJoystickName(name, key);
            return GroupJoystickDevices(new Dictionary<string, string> { [key] = name });
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
            var text = string.Join("\n", Text.Split('\n').Select(line =>
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
            return new SquadronsNativeDocument(text).GroupJoystickDevices();
        }
        /// <summary>Completes sparse native record arrays with disabled entries and groups declared controllers.
        /// Active record addresses and every field remain unchanged; completion affects the review copy only.</summary>
        public string PrepareReviewExport(Dictionary<string, string> names) =>
            new SquadronsNativeDocument(CompleteReviewBindingArrays()).GroupJoystickDevices(names);
        #endregion END Public
        #region BEGIN private helpers
        private string CompleteReviewBindingArrays()
        {
            var records = Bindings.ToList();
            foreach (var group in Bindings.GroupBy(row => row.Concept, StringComparer.Ordinal))
            {
                var indices = group.Select(row => row.Index).ToHashSet();
                var maximum = indices.Max();
                if (indices.Count == maximum + 1) continue;
                if (maximum >= 128) throw new InvalidOperationException("Cannot complete a sparse native binding array above index 127.");
                for (var index = 0; index <= maximum; index++)
                {
                    if (indices.Contains(index)) continue;
                    // Native v4 cleared-record form: type -1, no physical target and no active input.
                    records.Add(new(group.Key, index, new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["altbutton"] = 0, ["axis"] = 26, ["button"] = 174, ["deviceid"] = -1,
                        ["identifier"] = 0, ["modifier"] = 0, ["negate"] = 0, ["type"] = -1
                    }));
                }
            }
            if (records.Count == Bindings.Count) return Text;
            var newline = Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var parts = Text.Split('\n');
            var lines = parts.Select((line, index) => line + (index < parts.Length - 1 ? "\n" : "")).ToArray();
            bool RecordLine(string line) => line.StartsWith(BindingPrefix, StringComparison.Ordinal) &&
                !line.StartsWith(BindingPrefix + "InputDataVersion ", StringComparison.Ordinal);
            var first = Array.FindIndex(lines, RecordLine);
            var before = string.Concat(lines.Take(first));
            if (before.Length > 0 && !before.EndsWith('\n')) before += newline;
            var block = records.OrderBy(row => row.Concept, StringComparer.Ordinal).ThenBy(row => row.Index)
                .SelectMany(row => row.Fields.OrderBy(field => field.Key, StringComparer.Ordinal).Select(field =>
                    BindingPrefix + row.RecordId + "." + field.Key + " " + field.Value.ToString(CultureInfo.InvariantCulture)));
            var after = string.Concat(lines.Skip(first).Where(line => !RecordLine(line)));
            return before + string.Join(newline, block) + newline + after;
        }
        private void ValidateJoystickName(string name, string? currentKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name.Length > 256 || name != name.Trim() || name.Any(char.IsControl))
                throw new InvalidOperationException("Use a single-line game device name of at most 256 characters.");
            if (Values.Any(x => x.Key != currentKey && x.Key.StartsWith("GstInput.JoystickDevice", StringComparison.Ordinal) &&
                string.Equals(x.Value, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That game device name already has a slot. Link the existing slot, or use the correct distinct device name.");
        }
        private string GroupJoystickDevices(Dictionary<string, string>? changes = null)
        {
            const string prefix = "GstInput.JoystickDevice";
            var devices = Values.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            if (changes is not null) foreach (var pair in changes) devices[pair.Key] = pair.Value;
            if (devices.Count == 0) return Text;
            var parts = Text.Split('\n');
            var lines = parts.Select((line, index) => line + (index < parts.Length - 1 ? "\n" : "")).ToArray();
            var first = Math.Max(Array.FindIndex(lines, line => line.StartsWith(prefix, StringComparison.Ordinal)), 0);
            var bindings = Array.FindIndex(lines, line => line.StartsWith(BindingPrefix, StringComparison.Ordinal));
            if (bindings >= 0) first = Math.Min(first, bindings);
            var before = string.Concat(lines.Take(first));
            var after = string.Concat(lines.Skip(first).Where(line => !line.StartsWith(prefix, StringComparison.Ordinal)));
            var newline = Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var block = devices.OrderBy(x => int.Parse(x.Key[prefix.Length..], CultureInfo.InvariantCulture)).Select(x => x.Key + " " + x.Value);
            return before + string.Join(newline, block) + newline + after;
        }
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
