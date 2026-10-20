using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Heimdall.Application.DrawioMappingImporter;

namespace Heimdall.Application
{
    /// <summary>Renders bounded, non-bindable draw.io cover shapes and strokes without executing stencil content.</summary>
    internal static class DrawioArtworkExporter
    {
        private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
        #region BEGIN Public
        /// <summary>Detects ordinary edits requiring the version-four rendering contract.</summary>
        public static bool RequiresSupport(XElement graph)
        {
            var width = Number(graph, "pageWidth");
            var height = Number(graph, "pageHeight");
            return graph.Descendants("mxCell").Any(cell =>
            {
                var id = Id(cell);
                if (cell.Attribute("edge")?.Value == "1") return !id.StartsWith("connector-", StringComparison.Ordinal);
                if (cell.Attribute("vertex")?.Value != "1" || id is "canvas" or "footer" or "developer-notes" || id.StartsWith("edge-label-", StringComparison.Ordinal)) return false;
                if ((cell.Attribute("style")?.Value ?? "").Split(';').Contains("text", StringComparer.Ordinal) &&
                    !id.StartsWith("label-", StringComparison.Ordinal) && !id.StartsWith("presentation-", StringComparison.Ordinal)) return false;
                if (!id.StartsWith("label-", StringComparison.Ordinal) && !id.StartsWith("presentation-", StringComparison.Ordinal) &&
                    !id.StartsWith("hardware-image-", StringComparison.Ordinal) && !id.StartsWith("anchor-", StringComparison.Ordinal) &&
                    !id.StartsWith("mask-", StringComparison.Ordinal)) return true;
                var geometry = cell.Element("mxGeometry");
                return id.StartsWith("hardware-image-", StringComparison.Ordinal) && geometry is not null &&
                    (Number(geometry, "x") < 0 || Number(geometry, "y") < 0 ||
                     Number(geometry, "x") + Number(geometry, "width") > width || Number(geometry, "y") + Number(geometry, "height") > height);
            });
        }

        /// <summary>Resolves literal colors and draw.io theme pairs to an explicit, portable artwork color.</summary>
        public static string Color(XElement cell, string name, string fallback)
        {
            var value = Style(cell, name, fallback);
            var themed = Regex.Match(value, "^light-dark\\((#[A-Fa-f0-9]{6}|none|transparent),(#[A-Fa-f0-9]{6}|none|transparent)\\)$", RegexOptions.CultureInvariant);
            if (themed.Success)
                value = themed.Groups[1].Value is "none" or "transparent" ? themed.Groups[2].Value : themed.Groups[1].Value;
            if (!Regex.IsMatch(value, "^(#[A-Fa-f0-9]{6}|none|transparent)$", RegexOptions.CultureInvariant))
                throw new InvalidDataException(Id(cell) + ": artwork colors must be hex RGB, none, transparent or a draw.io theme pair.");
            return value;
        }

        /// <summary>Clips artwork to the original page without changing its geometry or any input marker.</summary>
        public static XElement Clip(XElement element) => new(Ns + "g", new XAttribute("clip-path", "url(#ipia-artwork-clip)"), element);

        /// <summary>Converts an ordinary cover rectangle or freehand stencil into explicit SVG primitives.</summary>
        public static XElement Vertex(XElement cell, HardwareGraphicControl geometry, ref int remainingCommands)
        {
            if (!string.IsNullOrEmpty(cell.Attribute("value")?.Value) || !string.IsNullOrEmpty(cell.Parent?.Attribute("label")?.Value))
                throw new InvalidDataException(Id(cell) + ": use a presentation-* text region for decorative text.");
            var shape = Style(cell, "shape", "");
            if (shape is "" or "rectangle")
                return Clip(new XElement(Ns + "rect", new XAttribute("id", Id(cell)),
                    new XAttribute("x", geometry.X), new XAttribute("y", geometry.Y),
                    new XAttribute("width", geometry.Width), new XAttribute("height", geometry.Height),
                    new XAttribute("fill", Color(cell, "fillColor", "#ffffff")),
                    new XAttribute("stroke", Color(cell, "strokeColor", "#000000")),
                    new XAttribute("stroke-width", StyleNumber(cell, "strokeWidth", 1, 0, 100))));
            if (!shape.StartsWith("stencil(", StringComparison.Ordinal) || !shape.EndsWith(')'))
                throw new InvalidDataException(Id(cell) + ": only cover rectangles and freehand stencils are supported as decorative shapes.");
            var stencil = Stencil(shape[8..^1]);
            var commands = stencil.Descendants("path").SelectMany(x => x.Elements()).ToArray();
            if (commands.Length == 0 || commands.Length > remainingCommands)
                throw new InvalidDataException("Freehand artwork exceeds its 100,000 path-command limit or has no path.");
            remainingCommands -= commands.Length;
            var paths = new XElement(Ns + "g", new XAttribute("id", Id(cell)),
                new XAttribute("fill", Style(cell, "lineShape", "") == "1" ? "none" : Color(cell, "fillColor", "none")),
                new XAttribute("stroke", Color(cell, "strokeColor", "#000000")),
                new XAttribute("stroke-width", StyleNumber(cell, "strokeWidth", 1, 0, 100)),
                new XAttribute("stroke-linecap", "round"), new XAttribute("stroke-linejoin", "round"));
            foreach (var path in stencil.Descendants("path"))
            {
                var data = new StringBuilder();
                foreach (var command in path.Elements())
                {
                    if (command.Name.LocalName == "close") { data.Append("Z "); continue; }
                    var x = Number(command, "x"); var y = Number(command, "y");
                    if (command.Attribute("x") is null || command.Attribute("y") is null || x < 0 || y < 0 || x > 100 || y > 100)
                        throw new InvalidDataException("Freehand coordinates must be finite values between zero and 100.");
                    data.Append(command.Name.LocalName == "move" ? "M " : "L ")
                        .Append((geometry.X + x * geometry.Width / 100).ToString("G17", CultureInfo.InvariantCulture)).Append(' ')
                        .Append((geometry.Y + y * geometry.Height / 100).ToString("G17", CultureInfo.InvariantCulture)).Append(' ');
                }
                paths.Add(new XElement(Ns + "path", new XAttribute("d", data.ToString().TrimEnd())));
            }
            return Clip(paths);
        }

        /// <summary>Renders a straight decorative line with explicit endpoints, waypoints or attached vertex ports.</summary>
        public static XElement Edge(XElement cell, IReadOnlyDictionary<string, XElement> cells, ref int remainingCommands)
        {
            if (cell.Attribute("parent")?.Value != "1" || Style(cell, "edgeStyle", "none") is not ("" or "none") || Style(cell, "endArrow", "none") != "none" ||
                Style(cell, "startArrow", "none") != "none" || Style(cell, "curved", "0") != "0" ||
                Style(cell, "rounded", "0") != "0" || !string.IsNullOrEmpty(cell.Attribute("value")?.Value))
                throw new InvalidDataException(Id(cell) + ": decorative lines must be straight, unlabelled and have no arrowheads.");
            var geometry = cell.Element("mxGeometry") ?? throw new InvalidDataException("A decorative line needs geometry.");
            var points = new List<(double X, double Y)> { Endpoint(cell, cells, geometry, "source", "exit") };
            points.AddRange(geometry.Elements("Array").Where(x => x.Attribute("as")?.Value == "points").Elements("mxPoint").Select(Point));
            points.Add(Endpoint(cell, cells, geometry, "target", "entry"));
            if (points.Count > remainingCommands) throw new InvalidDataException("Artwork exceeds its path-command limit.");
            remainingCommands -= points.Count;
            return Clip(new XElement(Ns + "polyline", new XAttribute("id", Id(cell)),
                new XAttribute("points", string.Join(' ', points.Select(p => p.X.ToString("G17", CultureInfo.InvariantCulture) + "," + p.Y.ToString("G17", CultureInfo.InvariantCulture)))),
                new XAttribute("fill", "none"), new XAttribute("stroke", Color(cell, "strokeColor", "#000000")),
                new XAttribute("stroke-width", StyleNumber(cell, "strokeWidth", 1, 0, 100))));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidateStencilNesting(XElement node, string name)
        {
            if (name is "move" or "line" or "close" && node.Parent?.Name != "path" ||
                name is "path" or "fillstroke" && node.Parent?.Name != "foreground")
                throw new InvalidDataException("Unsupported freehand stencil nesting.");
        }
        private static bool SupportedStencilAttribute(string name, XAttribute attribute) =>
            name == "shape" && attribute.Name == "strokewidth" && attribute.Value == "inherit" ||
            name is "move" or "line" && attribute.Name.LocalName is "x" or "y" && attribute.Name.Namespace == XNamespace.None;
        private static void ValidateStencilNode(XElement node)
        {
            var name = node.Name.LocalName;
            if (node.Name.Namespace != XNamespace.None || name is not ("shape" or "foreground" or "path" or "move" or "line" or "close" or "fillstroke"))
                throw new InvalidDataException("Unsupported freehand stencil command: " + node.Name);
            if (node.Attributes().Any(a => !SupportedStencilAttribute(name, a)))
                throw new InvalidDataException("Unsupported freehand stencil attributes.");
            ValidateStencilNesting(node, name);
        }
        private static XElement Stencil(string encoded)
        {
            if (encoded.Length > 1_000_000) throw new InvalidDataException("Freehand stencil exceeds 1 MB.");
            using var input = new MemoryStream(Convert.FromBase64String(encoded));
            using var inflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = inflate.Read(buffer)) > 0)
            {
                if (output.Length + read > 1_000_000) throw new InvalidDataException("Inflated freehand stencil exceeds 1 MB.");
                output.Write(buffer, 0, read);
            }
            var text = new UTF8Encoding(false, true).GetString(output.ToArray());
            if (Regex.IsMatch(text, "%(?![A-Fa-f0-9]{2})", RegexOptions.CultureInvariant)) throw new InvalidDataException("Invalid stencil URI encoding.");
            var shape = Parse(Uri.UnescapeDataString(text)).Root ?? throw new InvalidDataException("Empty freehand stencil.");
            if (shape.Name != "shape" || shape.Elements().Count() != 1 || shape.Element("foreground") is null)
                throw new InvalidDataException("Freehand stencils must contain one foreground.");
            foreach (var node in shape.DescendantsAndSelf())
            {
                ValidateStencilNode(node);
            }
            return shape;
        }
        private static (double X, double Y) Endpoint(XElement edge, IReadOnlyDictionary<string, XElement> cells,
            XElement geometry, string end, string port)
        {
            if (edge.Attribute(end)?.Value is { } id)
            {
                if (!cells.TryGetValue(id, out var vertex) || vertex.Attribute("vertex")?.Value != "1" || vertex.Attribute("parent")?.Value != "1")
                    throw new InvalidDataException("A decorative line references an unknown or grouped vertex: " + id);
                var bounds = vertex.Element("mxGeometry") ?? throw new InvalidDataException("An attached vertex needs geometry.");
                return CheckedPoint(Number(bounds, "x") + StyleNumber(edge, port + "X", 0.5, -4, 4) * Number(bounds, "width") + StyleNumber(edge, port + "Dx", 0, -4096, 4096),
                    Number(bounds, "y") + StyleNumber(edge, port + "Y", 0.5, -4, 4) * Number(bounds, "height") + StyleNumber(edge, port + "Dy", 0, -4096, 4096));
            }
            var point = geometry.Elements("mxPoint").SingleOrDefault(x => x.Attribute("as")?.Value == end + "Point")
                ?? throw new InvalidDataException("A decorative line needs a " + end + " endpoint.");
            return Point(point);
        }
        private static (double X, double Y) Point(XElement point)
        {
            if (point.Attribute("x") is null || point.Attribute("y") is null) throw new InvalidDataException("A line endpoint needs x and y coordinates.");
            return CheckedPoint(Number(point, "x"), Number(point, "y"));
        }
        private static (double X, double Y) CheckedPoint(double x, double y) =>
            Math.Abs(x) <= 8192 && Math.Abs(y) <= 8192 ? (x, y) : throw new InvalidDataException("Decorative line coordinates exceed the supported limit.");
        private static double StyleNumber(XElement cell, string name, double fallback, double minimum, double maximum)
        {
            var text = Style(cell, name, fallback.ToString(CultureInfo.InvariantCulture));
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= minimum && value <= maximum
                ? value : throw new InvalidDataException(Id(cell) + ": invalid " + name + ".");
        }
        #endregion END private helpers
    }
}
