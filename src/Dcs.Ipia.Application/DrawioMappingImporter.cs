using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Dcs.Ipia.Application
{
    /// <summary>Imports a bounded native draw.io subset into self-contained, deterministic user mappings.</summary>
    public static class DrawioMappingImporter
    {
        private const int MaximumSource = 2_000_000;
        private const int MaximumGraph = 4_000_000;
        private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
        #region BEGIN Public
        /// <summary>Finds safe local raster references without opening files or fetching URLs.</summary>
        /// <param name="source">Native or compressed editable source.</param>
        /// <returns>Distinct relative image paths that must be supplied explicitly.</returns>
        public static IReadOnlyList<string> LocalAssets(string source) => Cells(Graph(source))
            .Select(Image).Where(x => x.Length > 0 && !x.StartsWith("data:", StringComparison.Ordinal))
            .Select(SafeAsset).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        /// <summary>Adds explicit DCS input keys from a bundled drawing's existing catalog control map.</summary>
        /// <param name="source">Editable native or compressed drawing.</param>
        /// <param name="inputs">Trusted catalog marker-to-input mappings.</param>
        /// <returns>Self-contained editable source with existing explicit mappings preserved.</returns>
        public static string WithCatalogInputs(string source, IReadOnlyDictionary<string, string> inputs)
        {
            var graph = Graph(source);
            foreach (var cell in Cells(graph))
            {
                var id = Id(cell);
                if (!id.StartsWith("label-", StringComparison.Ordinal) || !inputs.TryGetValue(id[6..], out var key)) continue;
                var wrapper = cell.Parent?.Name.LocalName == "UserObject" ? cell.Parent : null;
                if (cell.Attribute("inputKey") is not null || wrapper?.Attribute("inputKey") is not null ||
                    Style(cell, "inputKey", "").Length > 0 || Style(cell, "buttonNumber", "").Length > 0) continue;
                cell.SetAttributeValue("inputKey", key);
            }
            return new XElement("mxfile", new XElement("diagram", new XElement(graph))).ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Finds one trusted bundled input map by stable markers and verified native keys, independent of filename.</summary>
        /// <param name="source">Editable drawing with preserved stable label IDs.</param>
        /// <param name="definition">Selected controller's verified native inputs.</param>
        /// <param name="candidates">Trusted installed marker-to-input maps.</param>
        /// <returns>One unambiguous compatible map, or null when no complete marker set matches.</returns>
        public static IReadOnlyDictionary<string, string>? ResolveCatalogInputs(string source, HardwareDefinition definition,
            IEnumerable<IReadOnlyDictionary<string, string>> candidates)
        {
            var markers = Cells(Graph(source)).Select(Id).Where(x => x.StartsWith("label-", StringComparison.Ordinal))
                .Select(x => x[6..]).ToHashSet(StringComparer.Ordinal);
            var matches = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var candidate in candidates.Where(x => markers.SetEquals(x.Keys)))
            {
                var mapped = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in candidate)
                {
                    var key = definition.Capabilities.DcsKeys.FirstOrDefault(x => DcsInputKey.SamePhysicalInput(x, pair.Value));
                    if (key is null) break;
                    mapped.Add(pair.Key, key);
                }
                if (mapped.Count == candidate.Count && mapped.Count > 0) matches.TryAdd(JsonSerializer.Serialize(mapped), mapped);
            }
            if (matches.Count > 1) throw new InvalidDataException("Drawing markers match conflicting bundled input maps. Export a starter or retain explicit inputKey metadata.");
            return matches.Values.SingleOrDefault();
        }

        /// <summary>Validates and renders exactly one diagram against verified target controls.</summary>
        /// <param name="source">Original editable source.</param>
        /// <param name="definition">Explicitly selected native capability definition.</param>
        /// <param name="sourceName">Display provenance, never an absolute source path.</param>
        /// <param name="applicationVersion">Importing application version.</param>
        /// <param name="assets">Explicit local raster bytes; external access is never performed.</param>
        /// <param name="exporterVersion">Versioned rendering semantics; saved revisions retain their original semantics.</param>
        /// <returns>Immutable revision ready for preview and explicit publication.</returns>
        public static UserMappingRevision Import(string source, HardwareDefinition definition, string sourceName,
            string applicationVersion, IReadOnlyDictionary<string, byte[]>? assets = null, string? exporterVersion = null)
        {
            var graph = Graph(source);
            exporterVersion ??= DrawioArtworkExporter.RequiresSupport(graph) ? "ipia-drawio-4" : Cells(graph).Any(cell => Style(cell, "ipiaTextLayout", "").Length > 0 ||
                Style(cell, "ipiaStatic", "") == "1") ? "ipia-drawio-3" : "ipia-drawio-2";
            if (exporterVersion is not ("ipia-drawio-1" or "ipia-drawio-2" or "ipia-drawio-3" or "ipia-drawio-4"))
                throw new InvalidDataException("Unsupported draw.io exporter version.");
            var compatible = exporterVersion != "ipia-drawio-1";
            var edited = exporterVersion == "ipia-drawio-4";
            var modern = exporterVersion is "ipia-drawio-3" or "ipia-drawio-4";
            HardwareInventoryContract.Validate(new(1, [definition], [], []));
            ValidateImportIdentity(sourceName, applicationVersion);
            var local = CaptureAssets(assets);
            var width = Number(graph, "pageWidth");
            var originalHeight = Number(graph, "pageHeight");
            ValidateCanvas(width, originalHeight);
            var cells = Cells(graph).ToArray();
            if (cells.Length > 2000 || cells.Any(x => string.IsNullOrWhiteSpace(Id(x))) ||
                cells.Select(Id).Distinct(StringComparer.Ordinal).Count() != cells.Length)
                throw new InvalidDataException("Use at most 2000 cells with unique stable IDs.");
            var byId = cells.ToDictionary(Id, StringComparer.Ordinal);
            if (!byId.ContainsKey("canvas") || !byId.ContainsKey("footer"))
                throw new InvalidDataException("The diagram needs canvas and footer cells.");
            var usedAssets = new HashSet<string>(StringComparer.Ordinal);
            var controls = new List<HardwareGraphicControl>();
            var mapped = new HashSet<string>(StringComparer.Ordinal);
            var svg = new XElement(SvgNamespace + "svg");
            var remainingArtworkCommands = 100_000;
            if (edited)
                svg.Add(new XElement(SvgNamespace + "defs", new XElement(SvgNamespace + "clipPath", new XAttribute("id", "ipia-artwork-clip"),
                    new XElement(SvgNamespace + "rect", new XAttribute("width", width), new XAttribute("height", originalHeight)))));
            AddVertices(svg, cells, byId, definition, local, usedAssets, controls, mapped, width, originalHeight,
                compatible, modern, edited, ref remainingArtworkCommands);
            if (mapped.Count == 0) throw new InvalidDataException("Map at least one label to an actual control.");
            AddEdges(svg, cells, byId, width, originalHeight, compatible, edited, ref remainingArtworkCommands);
            if (edited)
            {
                var order = cells.Select((cell, index) => (Id: Id(cell), Index: index)).ToDictionary(x => x.Id, x => x.Index, StringComparer.Ordinal);
                var artwork = svg.Elements().OrderBy(x => x.Attribute("data-ipia-source-cell") is { } id ? order[id.Value] : -1).ToArray();
                foreach (var element in artwork) element.Attribute("data-ipia-source-cell")?.Remove();
                svg.ReplaceNodes(artwork);
            }
            if (!usedAssets.SetEquals(local.Keys)) throw new InvalidDataException("Local assets must exactly match the diagram references.");
            var (finalWidth, finalHeight) = AddFallback(svg, definition, controls, mapped, width, originalHeight);
            svg.SetAttributeValue("viewBox", Point(0, 0).Replace(',', ' ') + " " + finalWidth.ToString(CultureInfo.InvariantCulture) + " " + finalHeight.ToString(CultureInfo.InvariantCulture));
            svg.SetAttributeValue("width", finalWidth); svg.SetAttributeValue("height", finalHeight);
            if (controls.Select(x => x.Control).Distinct(StringComparer.Ordinal).Count() != controls.Count)
                throw new InvalidDataException("A mapped marker conflicts with an unmapped input marker.");
            var rendered = svg.ToString(SaveOptions.DisableFormatting);
            if (rendered.Length > MaximumGraph) throw new InvalidDataException("Derived rendering exceeds 4 MB.");
            var revision = new UserMappingRevision("", sourceName, applicationVersion, definition, source, local, rendered,
                finalWidth, finalHeight, controls.OrderBy(x => x.Control, StringComparer.Ordinal).ToArray(),
                mapped.Order(StringComparer.Ordinal).ToArray(), Hash(source), Hash(rendered))
            { ExporterVersion = exporterVersion };
            return revision with { Id = Hash(JsonSerializer.Serialize(revision)) };
        }

        /// <summary>Reproduces every derived byte before trusting a persisted revision.</summary>
        /// <param name="revision">Untrusted library or snapshot revision.</param>
        public static void Validate(UserMappingRevision revision)
        {
            ArgumentNullException.ThrowIfNull(revision);
            if (revision.FormatVersion != 1 || revision.ExporterVersion is not ("ipia-drawio-1" or "ipia-drawio-2" or "ipia-drawio-3" or "ipia-drawio-4"))
                throw new InvalidDataException("Unsupported user mapping/exporter version.");
            var expected = Import(revision.Source, revision.Definition, revision.SourceName, revision.ApplicationVersion, revision.Assets, revision.ExporterVersion);
            if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(revision))
                throw new InvalidDataException("User mapping source, assets or derived rendering differs from its revision.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidateCanvas(double width, double originalHeight)
        {
            if (width < 100 || originalHeight < 100 || width > 4096 || originalHeight > 2048)
                throw new InvalidDataException("Canvas dimensions must be 100–4096 wide and 100–2048 high.");
        }
        private static SortedDictionary<string, byte[]> CaptureAssets(IReadOnlyDictionary<string, byte[]>? assets)
        {
            var local = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var item in assets ?? new Dictionary<string, byte[]>())
                local.Add(SafeAsset(item.Key), item.Value.ToArray());
            if (local.Count > 32 || local.Values.Sum(x => (long)x.Length) > 8_000_000)
                throw new InvalidDataException("Local images exceed the 32-file / 8 MB limit.");
            return local;
        }
        private static void ValidateImportIdentity(string sourceName, string applicationVersion)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || sourceName.Length > 200 || sourceName.Contains('/') ||
                sourceName.Contains('\\') || sourceName.Contains(':') || sourceName.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(applicationVersion))
                throw new InvalidDataException("Use a readable source filename and application version.");
        }
        private static string ResolveInputKey(XElement cell, HardwareDefinition definition, bool compatible)
        {
            var id = Id(cell);
            var marker = id[6..];
            if (!Regex.IsMatch(marker, "^[A-Za-z0-9_-]{1,100}$", RegexOptions.CultureInvariant))
                throw new InvalidDataException(id + ": use a stable ASCII label marker.");
            var wrapper = cell.Parent?.Name.LocalName == "UserObject" ? cell.Parent : null;
            var keys = new[] { wrapper?.Attribute("inputKey")?.Value, cell.Attribute("inputKey")?.Value,
                        Style(cell, "inputKey", "") }.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
            if (definition.Capabilities.DcsKeys.Contains(marker, StringComparer.Ordinal)) keys.Add(marker);
            var button = Style(cell, "buttonNumber", "");
            if (button.Length > 0) keys.Add("JOY_BTN" + button);
            if (compatible) keys = keys.Select(key => definition.Capabilities.DcsKeys
                .FirstOrDefault(actual => DcsInputKey.SamePhysicalInput(actual, key)) ?? key).ToList();
            if (keys.Distinct(StringComparer.Ordinal).Count() > 1)
                throw new InvalidDataException(id + ": conflicting explicit input mappings.");
            var key = keys.FirstOrDefault() ?? marker;
            if (compatible) key = definition.Capabilities.DcsKeys.FirstOrDefault(actual => DcsInputKey.SamePhysicalInput(actual, key)) ?? key;
            if (!definition.Capabilities.DcsKeys.Contains(key, StringComparer.Ordinal))
                throw new InvalidDataException(id + ": unknown input " + key + ". Set inputKey to an actual target DCS key.");
            return key;
        }
        private static void AddImage(XElement svg, XElement cell, HardwareGraphicControl geometry,
            IReadOnlyDictionary<string, byte[]> local, HashSet<string> usedAssets, bool edited, ref long rasterPixels, ref int imageCount)
        {
            var id = Id(cell);
            var reference = Image(cell);
            byte[] bytes;
            string mime;
            if (reference.StartsWith("data:", StringComparison.Ordinal))
            {
                var match = Regex.Match(reference, "^data:image/(png|jpeg|jpg)(?:;base64)?,([A-Za-z0-9+/=]+)$", RegexOptions.CultureInvariant);
                if (!match.Success) throw new InvalidDataException(id + ": only embedded PNG/JPEG images are supported.");
                bytes = Convert.FromBase64String(match.Groups[2].Value);
                mime = match.Groups[1].Value == "png" ? "png" : "jpeg";
                if (Convert.ToBase64String(bytes) != match.Groups[2].Value) throw new InvalidDataException("Noncanonical image encoding.");
            }
            else
            {
                var path = SafeAsset(reference);
                if (!local.TryGetValue(path, out bytes!)) throw new InvalidDataException("Supply the local image: " + path);
                usedAssets.Add(path);
                mime = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpeg";
            }
            rasterPixels += RasterImageContract.Validate(bytes, mime);
            if (++imageCount > 32 || rasterPixels > 8_000_000) throw new InvalidDataException("Diagram images exceed 32 rasters or 8 million total pixels.");
            var artwork = new XElement(SvgNamespace + "image", new XAttribute("href", "data:image/" + mime + ";base64," + Convert.ToBase64String(bytes)),
                new XAttribute("x", geometry.X), new XAttribute("y", geometry.Y),
                new XAttribute("width", geometry.Width), new XAttribute("height", geometry.Height),
                new XAttribute("preserveAspectRatio", "xMidYMid meet"));
            svg.Add(edited ? DrawioArtworkExporter.Clip(artwork) : artwork);
        }
        private static void AddControl(XElement svg, XElement cell, HardwareGraphicControl geometry, HardwareDefinition definition,
            List<HardwareGraphicControl> controls, HashSet<string> mapped, bool compatible, bool modern, bool edited)
        {
            var id = Id(cell);
            var marker = id[6..];
            var key = ResolveInputKey(cell, definition, compatible);
            if (!mapped.Add(key))
            {
                var existing = controls.Where(x => x.InputKey == key).ToArray();
                if (!compatible || existing.Length != 1 || !(existing[0].Control + "-shifted" == marker || marker + "-shifted" == existing[0].Control))
                    throw new InvalidDataException("Multiple labels map to " + key + ".");
            }
            controls.Add(geometry with
            {
                Control = marker,
                InputKey = key,
                Label = Text(cell, modern),
                IsAxis = definition.Capabilities.Inputs.Any(x => x.Kind == ControllerInputKind.Axis && x.DcsKey == key)
            });
            Label(svg, cell, geometry, "lbl-" + marker, compatible, modern, edited);
        }
        private static void ValidateGeometryBounds(string id, HardwareGraphicControl geometry, double width, double originalHeight, bool compatible, bool edited)
        {
            if (compatible && (id is "canvas" or "footer"))
            {
                if (Math.Abs(geometry.X) > 4096 || Math.Abs(geometry.Y) > 4096 || geometry.Width > 4096 || geometry.Height > 4096)
                    throw new InvalidDataException(id + ": canvas/footer geometry exceeds the supported limit.");
            }
            else if (edited && !id.StartsWith("label-", StringComparison.Ordinal) && !id.StartsWith("anchor-", StringComparison.Ordinal) &&
                !id.StartsWith("presentation-", StringComparison.Ordinal))
            {
                if (Math.Abs(geometry.X) > 4096 || Math.Abs(geometry.Y) > 4096 || geometry.Width > 4096 || geometry.Height > 4096)
                    throw new InvalidDataException(id + ": artwork geometry exceeds the supported limit.");
            }
            else if (geometry.X < 0 || geometry.Y < 0 || geometry.X + geometry.Width > width ||
                geometry.Y + geometry.Height > originalHeight)
                throw new InvalidDataException(id + ": geometry must fit inside the drawing canvas.");
        }
        private static bool SkipAuthoringVertex(XElement cell, IReadOnlyDictionary<string, XElement> byId, bool compatible)
        {
            var id = Id(cell);
            if (compatible && id.StartsWith("edge-label-", StringComparison.Ordinal))
            {
                var parent = cell.Attribute("parent")?.Value ?? "";
                if (!parent.StartsWith("connector-", StringComparison.Ordinal) ||
                    !byId.TryGetValue(parent, out var connector) || connector.Attribute("edge")?.Value != "1" ||
                    cell.Element("mxGeometry")?.Attribute("relative")?.Value != "1")
                    throw new InvalidDataException(id + ": edge annotations must belong to a callout connector.");
                Text(cell);
                return true; // The canonical shared exporter omits draw.io editor-only edge annotations.
            }
            if (cell.Attribute("parent")?.Value != "1" || cell.Element("mxGeometry")?.Attribute("relative")?.Value == "1")
                throw new InvalidDataException(id + ": grouped or relative vertices are unsupported.");
            if (id == "developer-notes") return true;
            return false;
        }
        private static void AddDecoration(XElement svg, XElement cell, HardwareGraphicControl geometry, double width, double originalHeight,
            bool compatible, bool modern, bool edited, ref int remainingArtworkCommands)
        {
            var id = Id(cell);
            if (id == "canvas")
                svg.Add(compatible ? new XElement(SvgNamespace + "rect", new XAttribute("width", width), new XAttribute("height", originalHeight),
                    new XAttribute("fill", Color(cell, "fillColor", "#0f172a"))) : Rect(geometry, Color(cell, "fillColor", "#0f172a")));
            else if (id.StartsWith("presentation-", StringComparison.Ordinal))
                Label(svg, cell, geometry, (modern && Style(cell, "ipiaStatic", "") == "1" ? "static-" : "meta-") + id[13..], compatible, modern, edited);
            else if (id.StartsWith("mask-", StringComparison.Ordinal))
            {
                var mask = compatible ? new XElement(SvgNamespace + "rect", new XAttribute("id", id), new XAttribute("x", geometry.X),
                    new XAttribute("y", geometry.Y), new XAttribute("width", geometry.Width), new XAttribute("height", geometry.Height),
                    new XAttribute("fill", edited ? DrawioArtworkExporter.Color(cell, "fillColor", "#ffffff") : Color(cell, "fillColor", "#ffffff")))
                    : Rect(geometry, Color(cell, "fillColor", "#ffffff"));
                svg.Add(edited ? DrawioArtworkExporter.Clip(mask) : mask);
            }
            else if (id.StartsWith("anchor-", StringComparison.Ordinal))
                svg.Add(new XElement(SvgNamespace + "circle", new XAttribute("cx", geometry.X + geometry.Width / 2),
                    new XAttribute("cy", geometry.Y + geometry.Height / 2), new XAttribute("r", 5), new XAttribute("fill", "#00bfff")));
            else if (id == "footer")
                svg.Add(new XElement(SvgNamespace + "text", new XAttribute("x", width / 2), new XAttribute("y", originalHeight - 8),
                    new XAttribute("text-anchor", "middle"), new XAttribute("font-size", 12), new XAttribute("fill", compatible ? "#475569" : "#94a3b8"), Text(cell)));
            else if (compatible && (cell.Attribute("style")?.Value ?? "").Split(';').Contains("text", StringComparer.Ordinal))
                Text(cell); // Retain bounded authoring notes in editable source, as the shared exporter does.
            else if (edited) svg.Add(DrawioArtworkExporter.Vertex(cell, geometry, ref remainingArtworkCommands));
            else throw new InvalidDataException(id + ": unsupported vertex. Use canvas, hardware-image-*, label-*, anchor-*, mask-*, presentation-* or footer.");
        }
        private static void AddVertices(XElement svg, XElement[] cells, IReadOnlyDictionary<string, XElement> byId, HardwareDefinition definition,
            IReadOnlyDictionary<string, byte[]> local, HashSet<string> usedAssets, List<HardwareGraphicControl> controls, HashSet<string> mapped,
            double width, double originalHeight, bool compatible, bool modern, bool edited, ref int remainingArtworkCommands)
        {
            long rasterPixels = 0;
            var imageCount = 0;
            foreach (var cell in cells.Where(x => x.Attribute("vertex")?.Value == "1"))
            {
                var id = Id(cell);
                var before = svg.Elements().Count();
                if (SkipAuthoringVertex(cell, byId, compatible)) continue;
                var geometry = Geometry(cell, compatible, edited);
                ValidateGeometryBounds(id, geometry, width, originalHeight, compatible, edited);
                if (id.StartsWith("hardware-image-", StringComparison.Ordinal) || edited && Style(cell, "shape", "") == "image")
                {
                    AddImage(svg, cell, geometry, local, usedAssets, edited, ref rasterPixels, ref imageCount);
                }
                else if (id.StartsWith("label-", StringComparison.Ordinal))
                {
                    AddControl(svg, cell, geometry, definition, controls, mapped, compatible, modern, edited);
                }
                else AddDecoration(svg, cell, geometry, width, originalHeight, compatible, modern, edited, ref remainingArtworkCommands);
                if (edited) MarkArtwork(svg, before, id);
            }
        }
        private static void AddConnector(XElement svg, XElement edge, IReadOnlyDictionary<string, XElement> byId,
            double width, double originalHeight, bool compatible)
        {
            var id = Id(edge);
            var marker = id.StartsWith("connector-", StringComparison.Ordinal) ? id[10..] : "";
            if (marker.Length == 0 || edge.Attribute("source")?.Value != "label-" + marker ||
                edge.Attribute("target")?.Value != "anchor-" + marker ||
                !byId.TryGetValue("label-" + marker, out var label) || !byId.TryGetValue("anchor-" + marker, out var anchor))
                throw new InvalidDataException(id + ": connect label-* to the matching anchor-*.");
            var a = Geometry(anchor, compatible); var l = Geometry(label, compatible);
            var points = new List<string> { Point(a.X + a.Width / 2, a.Y + a.Height / 2) };
            foreach (var p in edge.Descendants("mxPoint"))
            {
                var px = Number(p, "x"); var py = Number(p, "y");
                if (px < 0 || py < 0 || px > width || py > originalHeight) throw new InvalidDataException(id + ": connector point outside canvas.");
                points.Add(Point(px, py));
            }
            var ax = a.X + a.Width / 2; var ay = a.Y + a.Height / 2;
            var cx = l.X + l.Width / 2; var cy = l.Y + l.Height / 2;
            var horizontal = Math.Abs((ax - cx) / l.Width) > Math.Abs((ay - cy) / l.Height);
            points.Add(Point(horizontal ? (ax > cx ? l.X + l.Width : l.X) : cx,
                horizontal ? cy : (ay > cy ? l.Y + l.Height : l.Y)));
            svg.Add(new XElement(SvgNamespace + "polyline", new XAttribute("points", string.Join(' ', points)),
                new XAttribute("fill", "none"), new XAttribute("stroke", "#00bfff"), new XAttribute("stroke-width", 1.5),
                new XAttribute("stroke-dasharray", "5 3")));
            if (compatible) svg.Elements().Last().SetAttributeValue("opacity", "0.75");
        }
        private static void AddEdges(XElement svg, XElement[] cells, IReadOnlyDictionary<string, XElement> byId,
            double width, double originalHeight, bool compatible, bool edited, ref int remainingArtworkCommands)
        {
            foreach (var edge in cells.Where(x => x.Attribute("edge")?.Value == "1"))
            {
                var id = Id(edge);
                var before = svg.Elements().Count();
                if (edited && !id.StartsWith("connector-", StringComparison.Ordinal))
                {
                    svg.Add(DrawioArtworkExporter.Edge(edge, byId, ref remainingArtworkCommands));
                    MarkArtwork(svg, before, id);
                    continue;
                }
                AddConnector(svg, edge, byId, width, originalHeight, compatible);
                if (edited) MarkArtwork(svg, before, id);
            }
        }
        private static (double Width, double Height) AddFallback(XElement svg, HardwareDefinition definition,
            List<HardwareGraphicControl> controls, HashSet<string> mapped, double width, double originalHeight)
        {
            var fallback = GenericHardwareLayout.Create(definition).Diagram.Controls.Where(x => !mapped.Contains(x.InputKey)).ToArray();
            var columns = (fallback.Length + 31) / 32;
            var finalWidth = Math.Max(width, columns * 500);
            var finalHeight = originalHeight + (fallback.Length == 0 ? 0 : 50 + Math.Min(32, fallback.Length) * 48);
            if (finalWidth > 4096 || finalHeight > 4096) throw new InvalidDataException("Partial mapping fallback exceeds the 4096-pixel canvas limit.");
            if (fallback.Length > 0) svg.Add(Rect(new("fallback", "", "", 0, originalHeight, finalWidth, finalHeight - originalHeight), "#ffffff"));
            for (var index = 0; index < fallback.Length; index++)
            {
                var control = fallback[index] with { X = index / 32 * 500 + 170, Y = originalHeight + 50 + index % 32 * 48, Width = 320, Height = 42 };
                controls.Add(control);
                svg.Add(new XElement(SvgNamespace + "text", new XAttribute("x", control.X - 160), new XAttribute("y", control.Y + 27),
                    new XAttribute("font-size", 16), new XAttribute("fill", "#111111"), control.InputKey));
                svg.Add(Rect(control, "#ffffff"));
                svg.Add(new XElement(SvgNamespace + "text", new XAttribute("id", "lbl-" + control.Control),
                    new XAttribute("x", control.X + 8), new XAttribute("y", control.Y + 27), new XAttribute("font-size", 16), "Unassigned"));
            }
            return (finalWidth, finalHeight);
        }
        private static void ValidateGraphContent(XDocument document, XElement diagram, XElement graph)
        {
            var tags = new[] { "mxGraphModel", "root", "mxCell", "UserObject", "mxGeometry", "mxPoint", "Array" };
            foreach (var element in document.Descendants().Concat(graph.DescendantsAndSelf()))
            {
                if (element != document.Root && element != diagram && !tags.Contains(element.Name.LocalName, StringComparer.Ordinal))
                    throw new InvalidDataException("Unsupported diagram construct: " + element.Name);
                foreach (var attribute in element.Attributes())
                    if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        attribute.Name.LocalName is "link" or "href" ||
                        attribute.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        attribute.Value.Contains("<script", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Executable content and external links are unsupported.");
            }
        }
        private static XElement Graph(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || Encoding.UTF8.GetByteCount(source) > MaximumSource)
                throw new InvalidDataException("Draw.io source exceeds 2 MB or is empty.");
            var document = Parse(source.TrimStart('\ufeff'));
            if (document.Root?.Name != "mxfile" || document.Root.Elements("diagram").Count() != 1)
                throw new InvalidDataException("Import supports exactly one mxfile diagram.");
            var diagram = document.Root.Element("diagram")!;
            XElement graph;
            if (diagram.Elements().Any()) graph = diagram.Element("mxGraphModel") ?? throw new InvalidDataException("Expected mxGraphModel.");
            else
            {
                if (diagram.Value.Length > MaximumSource) throw new InvalidDataException("Compressed input exceeds 2 MB.");
                using var input = new MemoryStream(Convert.FromBase64String(diagram.Value.Trim()));
                using var inflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = inflate.Read(buffer)) > 0)
                {
                    if (output.Length + read > MaximumGraph) throw new InvalidDataException("Inflated diagram exceeds 4 MB.");
                    output.Write(buffer, 0, read);
                }
                var encoded = new UTF8Encoding(false, true).GetString(output.ToArray());
                if (Regex.IsMatch(encoded, "%(?![A-Fa-f0-9]{2})", RegexOptions.CultureInvariant))
                    throw new InvalidDataException("Invalid compressed URI encoding.");
                graph = Parse(Uri.UnescapeDataString(encoded)).Root ?? throw new InvalidDataException("Empty compressed graph.");
            }
            if (graph.Name != "mxGraphModel") throw new InvalidDataException("Expected mxGraphModel.");
            ValidateGraphContent(document, diagram, graph);
            return graph;
        }
        internal static XDocument Parse(string source)
        {
            using var reader = XmlReader.Create(new StringReader(source), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumGraph });
            return XDocument.Load(reader);
        }
        private static IEnumerable<XElement> Cells(XElement graph) => graph.Descendants("mxCell");
        internal static string Id(XElement cell) => cell.Attribute("id")?.Value ?? cell.Parent?.Attribute("id")?.Value ?? "";
        internal static double Number(XElement element, string name) => element.Attribute(name) is not { } attribute ? 0 :
            double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                ? value : throw new InvalidDataException("Invalid numeric geometry: " + name);
        private static HardwareGraphicControl Geometry(XElement cell, bool compatible, bool edited = false)
        {
            var geometry = cell.Element("mxGeometry") ?? throw new InvalidDataException(Id(cell) + ": missing geometry.");
            var result = new HardwareGraphicControl(Id(cell), "", "", Number(geometry, "x"), Number(geometry, "y"),
                Number(geometry, "width"), Number(geometry, "height"));
            if (result.Width <= 0 || result.Height <= 0) throw new InvalidDataException(Id(cell) + ": dimensions must be positive.");
            var style = cell.Attribute("style")?.Value ?? "";
            if (Style(cell, "shape", "") is not ("" or "image") && !(compatible && Style(cell, "shape", "") == "rectangle") && !(edited && Style(cell, "shape", "").StartsWith("stencil(", StringComparison.Ordinal))) throw new InvalidDataException(Id(cell) + ": custom shapes are unsupported.");
            if (Regex.IsMatch(style, "(?:^|;)(rotation|flipH|flipV|sketch|gradientColor)=", RegexOptions.CultureInvariant))
                throw new InvalidDataException(Id(cell) + ": transformed/custom shapes are unsupported.");
            return result;
        }
        private static string Image(XElement cell) => Regex.Match(cell.Attribute("style")?.Value ?? "",
            "(?:^|;)image=(.*?)(?=;(?!base64,)|$)", RegexOptions.CultureInvariant).Groups[1].Value;
        private static string SafeAsset(string path)
        {
            if (path.Length == 0 || path.Length > 200 || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(x => x.Length == 0 || x is "." or ".." || x.EndsWith('.') || x.EndsWith(' ')) ||
                path.Split('/').Any(x => Regex.IsMatch(x, "^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ||
                !Regex.IsMatch(path, "^[A-Za-z0-9_./ -]+\\.(png|jpe?g)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new InvalidDataException("Use a safe relative PNG/JPEG path; URLs and traversal are forbidden: " + path);
            return path;
        }
        internal static string Style(XElement cell, string name, string fallback) => (cell.Attribute("style")?.Value ?? "")
            .Split(';').FirstOrDefault(x => x.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..] ?? fallback;
        private static string Color(XElement cell, string name, string fallback)
        {
            var value = Style(cell, name, fallback);
            if (!Regex.IsMatch(value, "^(#[A-Fa-f0-9]{6}|none|transparent)$", RegexOptions.CultureInvariant))
                throw new InvalidDataException(Id(cell) + ": colors must be hex RGB or none.");
            return value;
        }
        private static string Text(XElement cell, bool multiline = false)
        {
            var value = cell.Attribute("value")?.Value ?? cell.Parent?.Attribute("label")?.Value ?? "";
            if (value.Length > 200 || value.Contains('<') || value.Contains('>') || value.Any(c => char.IsControl(c) && !(multiline && c == '\n')))
                throw new InvalidDataException(Id(cell) + ": use plain text of at most 200 characters.");
            return value.Length == 0 ? Id(cell) : value;
        }
        private static void MarkArtwork(XElement svg, int before, string id)
        {
            foreach (var element in svg.Elements().Skip(before)) element.SetAttributeValue("data-ipia-source-cell", id);
        }
        private static XElement Rect(HardwareGraphicControl g, string color) => new(SvgNamespace + "rect",
            new XAttribute("x", g.X), new XAttribute("y", g.Y), new XAttribute("width", g.Width), new XAttribute("height", g.Height),
            new XAttribute("fill", color), new XAttribute("stroke", "#00bfff"), new XAttribute("stroke-width", 1.5));
        private static void Label(XElement svg, XElement cell, HardwareGraphicControl g, string id, bool compatible, bool modern, bool edited = false)
        {
            var size = Style(cell, "fontSize", "16");
            if (!int.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 8 || number > 72)
                throw new InvalidDataException(Id(cell) + ": font size must be 8–72.");
            var fill = edited ? DrawioArtworkExporter.Color(cell, "fillColor", "#1e293b") : Color(cell, "fillColor", "#1e293b");
            var label = edited && Id(cell).StartsWith("presentation-", StringComparison.Ordinal) &&
                string.IsNullOrEmpty(cell.Attribute("value")?.Value ?? cell.Parent?.Attribute("label")?.Value) ? "" : Text(cell, modern);
            if (!compatible || fill is not ("none" or "transparent"))
            {
                var rectangle = Rect(g, fill);
                if (compatible)
                {
                    rectangle.SetAttributeValue("rx", 4);
                    rectangle.SetAttributeValue("stroke", edited ? DrawioArtworkExporter.Color(cell, "strokeColor", "#00bfff") : Color(cell, "strokeColor", "#00bfff"));
                }
                svg.Add(rectangle);
            }
            if (modern && Style(cell, "ipiaTextLayout", "") is "stacked" or "horizontal")
            {
                var layout = Style(cell, "ipiaTextLayout", "");
                var text = new XElement(SvgNamespace + "text", new XAttribute("id", id),
                    new XAttribute("x", g.X + g.Width / 2), new XAttribute("y", g.Y + number),
                    new XAttribute("text-anchor", "middle"), new XAttribute("font-size", number),
                    new XAttribute("fill", edited ? DrawioArtworkExporter.Color(cell, "fontColor", "#111111") : Color(cell, "fontColor", "#111111")),
                    new XAttribute("data-ipia-text-layout", layout), new XAttribute(XNamespace.Xml + "space", "preserve"));
                var lines = layout == "stacked" ? label.EnumerateRunes().Select(x => x.ToString()).ToArray() : label.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                    text.Add(new XElement(SvgNamespace + "tspan", new XAttribute("x", g.X + g.Width / 2),
                        new XAttribute("y", g.Y + number + i * number * 1.2), lines[i]));
                svg.Add(text);
                return;
            }
            svg.Add(new XElement(SvgNamespace + "text", new XAttribute("id", id),
                new XAttribute("x", g.X + g.Width / 2), new XAttribute("y", g.Y + g.Height / 2 + 4),
                new XAttribute("text-anchor", "middle"), new XAttribute("font-size", number),
                new XAttribute("fill", edited ? DrawioArtworkExporter.Color(cell, "fontColor", "#f1f5f9") : Color(cell, "fontColor", "#f1f5f9")), label));
        }
        private static string Point(double x, double y) => x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
        private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        #endregion END private helpers
    }
}

