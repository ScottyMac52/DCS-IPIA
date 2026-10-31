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
            var target = matches[0];
            if (!SupportedValue(target.Type, value))
                throw new ArgumentException("Replacement must be one supported value matching the captured native scalar type.", nameof(value));
            var output = _source[..target.Start] + value + _source[target.End..];
            if (Parse(output).HasOpaqueSyntax) throw new InvalidDataException("Replacement produced unsupported native syntax.");
            return output;
        }
        #endregion END Public
        #region BEGIN private helpers
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
            var stack = new Stack<string>();
            var pending = new List<string>();
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
                        stack.Push(pending[0]);
                        _blocks.Add(new(string.Join("/", stack.Reverse()), match.Index));
                        pending.Clear();
                        break;
                    case "}":
                        if (stack.Count == 0) throw new InvalidDataException("Unbalanced BLK block close.");
                        if (pending.Count != 0) HasOpaqueSyntax = true;
                        stack.Pop(); pending.Clear();
                        break;
                    case "=":
                        if (pending.Count != 1) throw new InvalidDataException("Unrecognized BLK scalar header.");
                        var header = pending[0]; var colon = header.LastIndexOf(':');
                        if (colon < 1 || colon != header.Length - 2) throw new InvalidDataException("Missing or unsupported BLK scalar type.");
                        var value = ReadValue(ref position);
                        var path = string.Join("/", stack.Reverse().Append(header[..colon]));
                        _scalars.Add(new(path, header[^1], value.Value, value.Index, value.Index + value.Length));
                        if (!SupportedValue(header[^1], value.Value)) HasOpaqueSyntax = true;
                        pending.Clear();
                        break;
                    case ";":
                        if (pending.Count != 0) HasOpaqueSyntax = true;
                        pending.Clear();
                        break;
                    default:
                        pending.Add(match.Value);
                        break;
                }
            }
            if (stack.Count != 0) throw new InvalidDataException("Unclosed BLK block.");
            if (pending.Count != 0) HasOpaqueSyntax = true;
        }
        #endregion END private helpers
    }
}
