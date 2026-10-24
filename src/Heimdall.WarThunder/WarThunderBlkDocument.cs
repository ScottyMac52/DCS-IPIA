using System.Globalization;
using System.Text.RegularExpressions;

namespace Heimdall.WarThunder;

/// <summary>
/// Reads native BLK controls without guessing the meaning of activation codes or device offsets.
/// Edits only existing scalar values, preserving all other source bytes and whitespace.
/// </summary>
public sealed class WarThunderBlkDocument
{
    private static readonly Regex Token = new(@"(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/)|(?<quoted>""(?:\\.|[^""\\])*"")|(?<word>[^\s{}=;""/]+)|(?<punct>[{}=;])", RegexOptions.Compiled);
    private readonly string _source;
    private readonly List<WarThunderBlkScalar> _scalars = [];
    private readonly List<WarThunderBlkBlock> _blocks = [];
    private WarThunderBlkDocument(string source) { _source = source; Scan(); }

    public static WarThunderBlkDocument Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length is 0 or > 8 * 1024 * 1024 || source.Contains('\0'))
            throw new InvalidDataException("BLK capture must contain 1–8 MiB of nonbinary text.");
        return new(source);
    }

    public IReadOnlyList<WarThunderBlkScalar> Scalars => _scalars;
    public IReadOnlyList<WarThunderBlkBlock> Blocks => _blocks;
    public string SerializeUnchanged() => _source;

    /// <summary>Replace exactly one existing typed scalar by its fully qualified path.</summary>
    public string ReplaceScalar(string path, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);
        if (value.IndexOfAny(['\r', '\n', '{', '}', ';']) >= 0)
            throw new ArgumentException("Replacement must be a single scalar.", nameof(value));
        var matches = _scalars.Where(x => x.Path == path).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Scalar path must identify exactly one captured value.");
        var target = matches[0];
        if (target.Type == 's' && !(value.Length >= 2 && value[0] == '"' && value[^1] == '"'))
            throw new ArgumentException("String scalars require a quoted native value.", nameof(value));
        if (target.Type is 'i' or 'b' && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("Integer scalar requires an integer value.", nameof(value));
        if (target.Type == 'r' && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("Real scalar requires a numeric value.", nameof(value));
        return _source[..target.Start] + value + _source[target.End..];
    }

    private void Scan()
    {
        var stack = new Stack<string>();
        var pending = new List<string>();
        int pos = 0;
        while (pos < _source.Length)
        {
            if (char.IsWhiteSpace(_source[pos])) { pos++; continue; }
            var match = Token.Match(_source, pos);
            if (!match.Success || match.Index != pos)
            {
                // Unknown syntax is retained, never silently normalized.
                pos++; pending.Clear(); continue;
            }
            pos += match.Length;
            if (match.Groups["comment"].Success) continue;
            var token = match.Value;
            if (token == "{")
            {
                var name = pending.Count == 1 ? pending[0] : "";
                if (name.Length == 0) throw new InvalidDataException("Unrecognized BLK block header.");
                stack.Push(name);
                _blocks.Add(new(string.Join("/", stack.Reverse()), match.Index));
                pending.Clear();
            }
            else if (token == "}")
            {
                if (stack.Count == 0) throw new InvalidDataException("Unbalanced BLK block close.");
                stack.Pop(); pending.Clear();
            }
            else if (token == "=")
            {
                if (pending.Count != 1) throw new InvalidDataException("Unrecognized BLK scalar header.");
                var header = pending[0]; var colon = header.LastIndexOf(':');
                if (colon < 1 || colon != header.Length - 2) throw new InvalidDataException("Missing BLK scalar type.");
                var next = Token.Match(_source, pos);
                while (next.Success && next.Index > pos && string.IsNullOrWhiteSpace(_source[pos..next.Index]))
                    break;
                if (!next.Success || !string.IsNullOrWhiteSpace(_source[pos..next.Index]) && next.Index != pos)
                    throw new InvalidDataException("Missing BLK scalar value.");
                if (next.Value is "{" or "}" or "=" or ";") throw new InvalidDataException("Invalid BLK scalar value.");
                var path = string.Join("/", stack.Reverse().Append(header[..colon]));
                _scalars.Add(new(path, header[^1], next.Value, next.Index, next.Index + next.Length));
                pos = next.Index + next.Length; pending.Clear();
            }
            else if (token == ";") pending.Clear();
            else pending.Add(token);
        }
        if (stack.Count != 0) throw new InvalidDataException("Unclosed BLK block.");
    }
}

public sealed record WarThunderBlkScalar(string Path, char Type, string Value, int Start, int End);
public sealed record WarThunderBlkBlock(string Path, int Start);
