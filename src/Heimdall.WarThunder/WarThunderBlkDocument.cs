using System.Globalization;
using System.Text.RegularExpressions;

namespace Heimdall.WarThunder
{
    /// <summary>Retains native BLK text and edits only uniquely addressed, supported captured scalar values.</summary>
    public sealed class WarThunderBlkDocument
    {
        private static readonly Regex Token = new(@"(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/)|(?<quoted>""(?:\\.|[^""\\])*"")|(?<word>[^\s{}=;""/]+)|(?<punct>[{}=;])", RegexOptions.Compiled);
        private readonly string _source;
        private readonly List<WarThunderBlkScalar> _scalars = [];
        private readonly List<WarThunderBlkBlock> _blocks = [];
        private WarThunderBlkDocument(string source) { _source = source; Scan(); }
        #region BEGIN Public
        /// <summary>Captures bounded nonbinary text; malformed supported structure is rejected.</summary>
        public static WarThunderBlkDocument Parse(string source)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source.Length is 0 or > 8 * 1024 * 1024 || source.Contains('\0'))
                throw new InvalidDataException("BLK capture must contain 1–8 MiB of nonbinary text.");
            return new(source);
        }
        /// <summary>Gets an immutable view of captured values and original source spans.</summary>
        public IReadOnlyList<WarThunderBlkScalar> Scalars => _scalars.AsReadOnly();
        /// <summary>Gets an immutable view of captured block paths, including repeated blocks.</summary>
        public IReadOnlyList<WarThunderBlkBlock> Blocks => _blocks.AsReadOnly();
        /// <summary>Gets whether retained syntax or scalar types exceed the editable parser subset.</summary>
        public bool HasOpaqueSyntax { get; private set; }
        /// <summary>Returns every captured character unchanged, including unsupported syntax and comments.</summary>
        public string SerializeUnchanged() => _source;
        /// <summary>Replaces one existing value; opaque syntax, ambiguous paths and invalid typed values block editing.</summary>
        public string ReplaceScalar(string path, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(value);
            if (HasOpaqueSyntax) throw new InvalidOperationException("This capture contains unsupported syntax. Unchanged export remains available; scalar edits are blocked.");
            var matches = _scalars.Where(x => x.Path == path).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Scalar path must identify exactly one captured value.");
            return ReplaceOccurrences(new Dictionary<string, string> { [matches[0].Address] = value });
        }
        /// <summary>Changes explicitly addressed scalar occurrences atomically, including repeated native paths.</summary>
        public string ReplaceOccurrences(IReadOnlyDictionary<string, string> replacements)
        {
            ArgumentNullException.ThrowIfNull(replacements);
            RequireEditable();
            var changes = new List<(int Start, int End, string Text)>();
            foreach (var (address, value) in replacements)
            {
                var target = _scalars.SingleOrDefault(item => item.Address == address)
                    ?? throw new InvalidOperationException("Unknown native scalar occurrence.");
                ArgumentNullException.ThrowIfNull(value);
                if (!SupportedValue(target.Type, value)) throw new ArgumentException("Replacement must match the captured scalar type.", nameof(replacements));
                changes.Add((target.Start, target.End, value));
            }
            return Write(changes);
        }
        /// <summary>Removes one complete explicitly addressed block, retaining all source outside its span.</summary>
        public string RemoveBlock(string address)
        {
            RequireEditable();
            var block = Block(address);
            return Write([(block.HeaderStart, block.End, "")]);
        }
        /// <summary>Clears a block body while retaining its explicit native command header.</summary>
        public string EmptyBlock(string address)
        {
            RequireEditable();
            var block = Block(address);
            return Write([(block.Start + 1, block.End - 1, "")]);
        }
        /// <summary>Appends a validated typed scalar without rewriting surrounding source.</summary>
        public string AppendScalar(string address, string name, char type, string value) =>
            Append(address, Field(name, type, value));
        /// <summary>Appends a named child block containing validated typed fields.</summary>
        public string AppendChild(string address, string name, IReadOnlyDictionary<string, (char Type, string Value)> fields)
        {
            ArgumentNullException.ThrowIfNull(fields);
            Identifier(name);
            return Append(address, name + "{" + string.Concat(fields.Select(field => Field(field.Key, field.Value.Type, field.Value.Value))) + "}");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Identifier(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
                throw new ArgumentException("Unsupported native member name.", nameof(name));
        }
        private static string Field(string name, char type, string value)
        {
            Identifier(name); ArgumentNullException.ThrowIfNull(value);
            if (!SupportedValue(type, value)) throw new ArgumentException("Unsupported native scalar value.", nameof(value));
            return name + ":" + type + "=" + value + ";";
        }
        private string Append(string address, string member)
        {
            RequireEditable();
            var block = Block(address);
            var newline = _source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            return Write([(block.End - 1, block.End - 1, newline + "  " + member + newline)]);
        }
        private void RequireEditable()
        {
            if (HasOpaqueSyntax) throw new InvalidOperationException("Unsupported native syntax blocks editing.");
        }
        private WarThunderBlkBlock Block(string address) => _blocks.SingleOrDefault(block => block.Address == address)
            ?? throw new InvalidOperationException("Unknown native block occurrence.");
        private string Write(IEnumerable<(int Start, int End, string Text)> changes)
        {
            var output = _source;
            foreach (var change in changes.OrderByDescending(change => change.Start))
                output = output[..change.Start] + change.Text + output[change.End..];
            if (Parse(output).HasOpaqueSyntax) throw new InvalidDataException("Native edit produced unsupported syntax.");
            return output;
        }
        private static bool SupportedValue(char type, string value)
        {
            if (value.IndexOfAny(['\r', '\n', '\0']) >= 0) return false;
            var match = Token.Match(value);
            if (!match.Success || match.Index != 0 || match.Length != value.Length || match.Groups["comment"].Success) return false;
            return type switch
            {
                's' or 't' => match.Groups["quoted"].Success,
                'i' => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                'b' => value is "yes" or "no" or "0" or "1",
                'r' => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number),
                _ => false
            };
        }
        private Match ReadValue(ref int position)
        {
            while (position < _source.Length)
            {
                if (char.IsWhiteSpace(_source[position])) { position++; continue; }
                var match = Token.Match(_source, position);
                if (!match.Success || match.Index != position) break;
                position += match.Length;
                if (match.Groups["comment"].Success) continue;
                if (match.Groups["punct"].Success) break;
                return match;
            }
            throw new InvalidDataException("Missing or unsupported BLK scalar value.");
        }
        private void Scan()
        {
            var stack = new Stack<int>();
            var pending = new List<(string Text, int Start)>();
            var occurrences = new Dictionary<(string Parent, string Name, bool Block), int>();
            string Address(string parent, string name, bool block)
            {
                var key = (parent, name, block);
                var index = occurrences.GetValueOrDefault(key);
                occurrences[key] = index + 1;
                return (block ? "b:" : "s:") + (parent.Length == 0 ? "" : parent[2..] + "/")
                    + Uri.EscapeDataString(name) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            }
            var position = 0;
            while (position < _source.Length)
            {
                if (char.IsWhiteSpace(_source[position])) { position++; continue; }
                var match = Token.Match(_source, position);
                if (!match.Success || match.Index != position)
                {
                    HasOpaqueSyntax = true; position++; pending.Clear(); continue;
                }
                position += match.Length;
                if (match.Groups["comment"].Success) continue;
                switch (match.Value)
                {
                    case "{":
                        if (pending.Count != 1) throw new InvalidDataException("Unrecognized BLK block header.");
                        var parent = stack.Count == 0 ? null : _blocks[stack.Peek()];
                        var block = new WarThunderBlkBlock((parent is null ? "" : parent.Path + "/") + pending[0].Text, match.Index)
                        {
                            Address = Address(parent?.Address ?? "", pending[0].Text, true), ParentAddress = parent?.Address ?? "",
                            HeaderStart = pending[0].Start
                        };
                        stack.Push(_blocks.Count); _blocks.Add(block);
                        pending.Clear();
                        break;
                    case "}":
                        if (stack.Count == 0) throw new InvalidDataException("Unbalanced BLK block close.");
                        if (pending.Count != 0) HasOpaqueSyntax = true;
                        var closed = stack.Pop();
                        _blocks[closed] = _blocks[closed] with { End = match.Index + match.Length };
                        pending.Clear();
                        break;
                    case "=":
                        if (pending.Count != 1) throw new InvalidDataException("Unrecognized BLK scalar header.");
                        var header = pending[0].Text; var colon = header.LastIndexOf(':');
                        if (colon < 1 || colon != header.Length - 2) throw new InvalidDataException("Missing or unsupported BLK scalar type.");
                        var value = ReadValue(ref position);
                        var owner = stack.Count == 0 ? null : _blocks[stack.Peek()];
                        var path = (owner is null ? "" : owner.Path + "/") + header[..colon];
                        _scalars.Add(new(path, header[^1], value.Value, value.Index, value.Index + value.Length)
                        {
                            Address = Address(owner?.Address ?? "", header[..colon], false), BlockAddress = owner?.Address ?? ""
                        });
                        if (!SupportedValue(header[^1], value.Value)) HasOpaqueSyntax = true;
                        pending.Clear();
                        break;
                    case ";":
                        if (pending.Count != 0) HasOpaqueSyntax = true;
                        pending.Clear();
                        break;
                    default:
                        pending.Add((match.Value, match.Index));
                        break;
                }
            }
            if (stack.Count != 0) throw new InvalidDataException("Unclosed BLK block.");
            if (pending.Count != 0) HasOpaqueSyntax = true;
        }
        #endregion END private helpers
    }
}
