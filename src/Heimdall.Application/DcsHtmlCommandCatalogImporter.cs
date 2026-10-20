using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Heimdall.Application
{
    /// <summary>Reads DCS Controls HTML exports into the same named-category module palette as a saved catalog.</summary>
    public static class DcsHtmlCommandCatalogImporter
    {
        #region BEGIN Public
        /// <summary>Imports one or more DCS-generated HTML exports for the selected aircraft.</summary>
        /// <param name="files">Filename and HTML content for each selected device export.</param>
        /// <returns>Distinct assignable commands whose category comes from the DCS category column.</returns>
        public static IReadOnlyList<CommandPaletteEntry> Parse(IEnumerable<(string FileName, string Html)> files)
        {
            ArgumentNullException.ThrowIfNull(files);
            var result = new Dictionary<string, CommandPaletteEntry>(StringComparer.Ordinal);
            var any = false;
            foreach (var (fileName, html) in files)
            {
                any = true;
                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(html) ||
                    !(fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                      fileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Select DCS Controls HTML exports (.html or .htm).");
                var heading = Heading.Match(html);
                var device = heading.Success ? Clean(heading.Groups[1].Value) : string.Empty;
                if (!string.Equals(device, Path.GetFileNameWithoutExtension(fileName),
                                   StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"{fileName}: DCS device heading must match the filename.");
                var count = 0;
                var fileKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match row in Rows.Matches(html))
                {
                    var cells = Cells.Matches(row.Groups[1].Value)
                                    .Cast<Match>()
                                    .Select(x => Clean(x.Groups[1].Value))
                                    .ToArray();
                    if (cells.Length == 0)
                        continue;
                    count++;
                    if (cells.Length != 4 || string.IsNullOrWhiteSpace(cells[1]) || string.IsNullOrWhiteSpace(cells[3]))
                        throw new InvalidDataException(
                            $"{fileName}: DCS command row {count} needs four columns, a name and a hash.");
                    var id = cells[3];
                    if (!fileKeys.Add(id))
                        throw new InvalidDataException($"{fileName}: duplicate command hash '{id}'.");
                    var axis = AxisHash.IsMatch(id);
                    if (!axis && !ButtonHash.IsMatch(id))
                        throw new InvalidDataException($"{fileName}: unsupported command hash '{id}'.");
                    var categories =
                        cells[2].Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    var category = categories.Length == 0 ? "Uncategorized" : string.Join(" / ", categories);
                    var entry = new CommandPaletteEntry(id, cells[1], category, axis);
                    if (result.TryGetValue(id, out var prior) && prior != entry)
                        throw new InvalidDataException(
                            $"{fileName}: conflicting name, category or type for command '{id}'.");
                    result[id] = entry;
                }
                if (count == 0)
                    throw new InvalidDataException($"{fileName}: no DCS command rows found.");
            }
            if (!any)
                throw new InvalidDataException("Select one or more DCS Controls HTML exports.");
            return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Clean(string html)
        {
            var value = WebUtility.HtmlDecode(Tags.Replace(Breaks.Replace(html, " "), string.Empty))
                            .Replace('\u00a0', ' ')
                            .Trim();
            return Whitespace.Replace(value, " ");
        }
        private static readonly Regex Heading =
            new("<h1[^>]*>([\\s\\S]*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        private static readonly Regex Rows =
            new("<tr[^>]*>([\\s\\S]*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        private static readonly Regex Cells =
            new("<td[^>]*>([\\s\\S]*?)</td>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        private static readonly Regex Tags = new("<[^>]+>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex Breaks =
            new("<br\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex Whitespace = new("\\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex AxisHash =
            new("^a.+cd(?:nil|-?\\d+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex ButtonHash =
            new("^d.+p.+u.+cd.+vd.+vp.+vu.+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        #endregion END private helpers
    }
}
