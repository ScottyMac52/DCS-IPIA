using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Tests ordered event selection and portable Windows clipboard table data.</summary>
    public sealed class InputTestLogFormatterTests
    {
        #region BEGIN Public
        /// <summary>No selection copies all rows; selected rows follow displayed rather than selection order.</summary>
        [Fact]
        public void SelectsRowsInDisplayedOrder()
        {
            var first = Entry("First");
            var second = Entry("Second");
            var third = Entry("Third");
            InputTestLogEntry[] rows = [first, second, third];
            Assert.Equal(rows, InputTestLogFormatter.SelectRows(rows, []));
            Assert.Equal([first, third], InputTestLogFormatter.SelectRows(rows, [third, first]));
            Assert.Same(second, Assert.Single(InputTestLogFormatter.SelectRows(rows, [second])));
            Assert.Empty(InputTestLogFormatter.SelectRows([], []));
            Assert.Empty(InputTestLogFormatter.SelectRows(rows, [Entry("Absent")]));
        }
        /// <summary>Distinct event instances with identical data remain independently selectable.</summary>
        [Fact]
        public void KeepsDuplicateEventsIndependentlySelectable()
        {
            var first = Entry("Same");
            var duplicate = first with { };
            Assert.Equal(first, duplicate);
            Assert.Same(duplicate, Assert.Single(InputTestLogFormatter.SelectRows([first, duplicate], [duplicate])));
        }
        /// <summary>Clipboard HTML escapes data while preserving column and row order.</summary>
        [Fact]
        public void EscapesHtmlAndPreservesColumns()
        {
            var row = Entry("Pitch <script>&\"' ✈") with { InputKey = "JOY_<X>", ActiveModifiers = "S3 & Base" };
            var result = InputTestLogFormatter.Format([row, Entry("Second")]);
            var table = XElement.Parse(Fragment(result.Html));
            Assert.Equal(["Time", "Device", "Input", "Transition", "Value", "Active modifiers", "Commands"],
                table.Element("thead")!.Element("tr")!.Elements("th").Select(x => x.Value));
            var rows = table.Element("tbody")!.Elements("tr").ToArray();
            Assert.Equal(2, rows.Length);
            Assert.Equal(["12:34:56.789", row.Device, "JOY_<X>", "Press", "1.000", "S3 & Base", row.Commands],
                rows[0].Elements("td").Select(x => x.Value));
            Assert.Equal("Second", rows[1].Elements("td").Last().Value);
            Assert.Contains("&lt;script&gt;&amp;&quot;&#39;", result.Html);
            Assert.DoesNotContain("<script>", result.Html);
        }
        /// <summary>Offsets count UTF-8 bytes, including multibyte device input and command text.</summary>
        [Fact]
        public void UsesUtf8ByteOffsets()
        {
            var content = InputTestLogFormatter.Format([Entry("移動 é 🚀")]).Html;
            var bytes = Encoding.UTF8.GetBytes(content);
            var startHtml = Offset(content, "StartHTML");
            var endHtml = Offset(content, "EndHTML");
            var start = Offset(content, "StartFragment");
            var end = Offset(content, "EndFragment");
            Assert.Equal("<html><body><!--StartFragment-->", Encoding.UTF8.GetString(bytes[startHtml..start]));
            Assert.Equal("<!--EndFragment--></body></html>", Encoding.UTF8.GetString(bytes[end..endHtml]));
            Assert.Equal(bytes.Length, endHtml);
            Assert.True(endHtml > content.Length);
            Assert.StartsWith("<table", Fragment(content));
            Assert.EndsWith("</table>", Fragment(content));
            Assert.Contains("移動 é 🚀", Fragment(content));
        }
        /// <summary>Plain text includes headers, normalized values and sanitized cell separators.</summary>
        [Fact]
        public void ProvidesPlainTextWithoutSplittingCells()
        {
            var row = Entry("First\tSecond\r\nThird") with { PhysicalGuid = Guid.Empty, Value = 0.125 };
            var result = InputTestLogFormatter.Format([row]);
            var lines = result.Text.Split("\r\n");
            Assert.Equal(2, lines.Length);
            Assert.Equal(7, lines[0].Split('\t').Length);
            Assert.Equal(["12:34:56.789", "Keyboard", "JOY_BTN1", "Press", "0.125", "Module:Base", "First Second  Third"], lines[1].Split('\t'));
            Assert.Contains("Keyboard", row.Display);
            Assert.Contains("0.125", row.ToString());
        }
        /// <summary>Empty data still produces a valid table and text header without fabricated events.</summary>
        [Fact]
        public void FormatsAnEmptyTable()
        {
            var result = InputTestLogFormatter.Format([]);
            Assert.Empty(XElement.Parse(Fragment(result.Html)).Element("tbody")!.Elements());
            Assert.DoesNotContain("\r\n", result.Text);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static InputTestLogEntry Entry(string commands) => new(new DateTime(2026, 10, 5, 12, 34, 56, 789),
            Guid.Parse("10000000-0000-0000-0000-000000000001"), "JOY_BTN1", "Press", 1, "Module:Base", commands);
        private static int Offset(string value, string name) => int.Parse(value.Split("\r\n").Single(x => x.StartsWith(name + ":", StringComparison.Ordinal))[(name.Length + 1)..], CultureInfo.InvariantCulture);
        private static string Fragment(string html) => Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(html)[Offset(html, "StartFragment")..Offset(html, "EndFragment")]);
        #endregion END private helpers
    }
}
