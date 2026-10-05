using System.Globalization;
using System.Text;

namespace Dcs.Ipia.Application
{
    /// <summary>Exports selected input-test events without changing their session or displayed order.</summary>
    public static class InputTestLogFormatter
    {
        private static readonly string[] Headers = ["Time", "Device", "Input", "Transition", "Value", "Active modifiers", "Commands"];
        private const string HeaderTemplate = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        #region BEGIN Public
        /// <summary>Selects only chosen events, or all events when no rows are selected.</summary>
        /// <param name="displayed">Events in displayed order.</param>
        /// <param name="selected">Selected event instances, in any order.</param>
        /// <returns>Events in displayed order, preserving duplicate event instances.</returns>
        public static IReadOnlyList<InputTestLogEntry> SelectRows(IReadOnlyList<InputTestLogEntry> displayed, IReadOnlyList<InputTestLogEntry> selected)
        {
            if (selected.Count == 0) return displayed.ToArray();
            var chosen = new HashSet<InputTestLogEntry>(selected, ReferenceEqualityComparer.Instance);
            return displayed.Where(chosen.Contains).ToArray();
        }
        /// <summary>Builds an escaped HTML table and a plain-text table for the Windows clipboard.</summary>
        /// <param name="entries">Events in the order they should be copied.</param>
        /// <returns>HTML clipboard data with valid byte offsets and plain text.</returns>
        public static InputTestLogClipboard Format(IReadOnlyList<InputTestLogEntry> entries)
        {
            var table = new StringBuilder("<table border=\"1\"><thead><tr>");
            foreach (var header in Headers) table.Append("<th>").Append(Encode(header)).Append("</th>");
            table.Append("</tr></thead><tbody>");
            var text = new StringBuilder(string.Join('\t', Headers));
            foreach (var entry in entries)
            {
                var cells = Cells(entry);
                table.Append("<tr>");
                foreach (var cell in cells) table.Append("<td>").Append(Encode(cell)).Append("</td>");
                table.Append("</tr>");
                text.Append("\r\n").Append(string.Join('\t', cells.Select(x => x.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '))));
            }
            table.Append("</tbody></table>");
            const string prefix = "<html><body><!--StartFragment-->";
            const string suffix = "<!--EndFragment--></body></html>";
            var startHtml = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, HeaderTemplate, 0, 0, 0, 0));
            var startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
            var endFragment = startFragment + Encoding.UTF8.GetByteCount(table.ToString());
            var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);
            var headerText = string.Format(CultureInfo.InvariantCulture, HeaderTemplate, startHtml, endHtml, startFragment, endFragment);
            return new(headerText + prefix + table + suffix, text.ToString());
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string[] Cells(InputTestLogEntry entry) => [entry.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            entry.Device, entry.InputKey, entry.Transition, entry.Value.ToString("F3", CultureInfo.InvariantCulture), entry.ActiveModifiers, entry.Commands];
        private static string Encode(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&#39;", StringComparison.Ordinal);
        #endregion END private helpers
    }
}
