using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Rendering;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Reproduces ordinary draw.io edits using the exact MFD files reported by the user.</summary>
    public sealed class EditedDrawioImportTests
    {
        #region BEGIN Public
        /// <summary>Image overhangs, cover rectangles, freehand strokes and straight lines survive preview and persistence.</summary>
        /// <param name="file">Unmodified uploaded drawing.</param>
        [Theory]
        [InlineData("edited-tm-mfd.drawio")]
        [InlineData("edited-tm-mfd-2.drawio")]
        public async Task Reported_drawings_import_with_all_controls_and_artwork(string file)
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!
                .First(x => x.DeviceId == "tm-mfd");
            var inputs = hardware.Diagram.Controls.ToDictionary(x => x.Control, x => x.InputKey, StringComparer.Ordinal);
            var definition = BundledDrawingImportTests.Definition(inputs.Values);
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CustomMappings", file);
            var revision = DrawioMappingFileImport.Load(path, definition, "test", catalogCandidates: [inputs]);
            Assert.Equal("ipia-drawio-4", revision.ExporterVersion);
            Assert.Equal(48, revision.Controls.Count);
            Assert.Equal(28, revision.MappedInputs.Count);
            Assert.Equal(inputs.Keys.Order(StringComparer.Ordinal), revision.Controls.Select(x => x.Control).Order(StringComparer.Ordinal));
            Assert.Equal(960, revision.Width);
            Assert.Equal(720, revision.Height);
            var svg = XDocument.Parse(revision.Svg);
            XNamespace ns = "http://www.w3.org/2000/svg";
            Assert.Equal(file == "edited-tm-mfd.drawio" ? 15 : 63, svg.Descendants(ns + "path").Count());
            Assert.Equal(4, svg.Descendants(ns + "polyline").Count());
            Assert.Contains(svg.Descendants(ns + "rect"), x => x.Attribute("fill")?.Value == "#D5F6FF");
            Assert.All(svg.Descendants(ns + "text").Where(x => x.Attribute("id")?.Value.StartsWith("meta-", StringComparison.Ordinal) == true),
                x => Assert.Equal("", x.Value));
            var png = CanonicalSvgRenderer.RenderComplete(revision.Svg, revision.Width, revision.Height);
            Assert.True(png.Length > 10_000);
            var output = Environment.GetEnvironmentVariable("IPIA_EDITED_DRAWIO_PREVIEW");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                await File.WriteAllBytesAsync(Path.Combine(output, file + ".png"), png);
            }
            var root = Path.Combine(Path.GetTempPath(), "ipia-edited-drawio-" + Guid.NewGuid().ToString("N"));
            try
            {
                var library = new FileUserMappingLibrary(root);
                await library.SaveAsync(revision);
                var reopened = Assert.Single(await library.ListAsync());
                Assert.Equal(revision.Svg, reopened.Svg);
                Assert.Equal(revision.Id, reopened.Id);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        /// <summary>Renamed bundled copies use only unambiguous stable-marker evidence, never displayed label text.</summary>
        [Fact]
        public void Catalog_resolution_rejects_ambiguity_and_preserves_explicit_keys()
        {
            var source = DrawioMappingFixture.Source();
            var definition = GenericHardwareFixture.Definition();
            var first = new Dictionary<string, string>(StringComparer.Ordinal) { ["trigger"] = "JOY_BTN1", ["JOY_X"] = "JOY_X" };
            Assert.Equal(first.OrderBy(x => x.Key, StringComparer.Ordinal), DrawioMappingImporter.ResolveCatalogInputs(source, definition, [first, first])!.OrderBy(x => x.Key, StringComparer.Ordinal));
            Assert.Null(DrawioMappingImporter.ResolveCatalogInputs(source, definition, []));
            Assert.Null(DrawioMappingImporter.ResolveCatalogInputs(source, definition, [new Dictionary<string, string> { ["other"] = "JOY_BTN1" }]));
            Assert.Null(DrawioMappingImporter.ResolveCatalogInputs(source, definition, [new Dictionary<string, string> { ["trigger"] = "JOY_BTN999", ["JOY_X"] = "JOY_X" }]));
            var second = new Dictionary<string, string>(first) { ["trigger"] = "JOY_BTN2" };
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.ResolveCatalogInputs(source, definition, [first, second]));
            var enriched = DrawioMappingImporter.WithCatalogInputs(source, second);
            Assert.Equal("JOY_BTN1", XDocument.Parse(enriched).Descendants("UserObject").Single().Attribute("inputKey")!.Value);
        }

        /// <summary>Sources with explicit keys import without a matching installed catalog and with empty candidate sets.</summary>
        [Fact]
        public void Explicit_sources_need_no_catalog_match()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-explicit-" + Guid.NewGuid().ToString("N") + ".drawio");
            try
            {
                File.WriteAllText(path, DrawioMappingFixture.Source());
                var result = DrawioMappingFileImport.Load(path, GenericHardwareFixture.Definition(), "test", catalogCandidates: []);
                Assert.Equal("ipia-drawio-2", result.ExporterVersion);
                Assert.Contains(result.Controls, x => x.Control == "trigger" && x.InputKey == "JOY_BTN1");
            }
            finally { File.Delete(path); }
        }

        /// <summary>Aggregate freehand and line budgets bound work even when every individual compressed stencil is small.</summary>
        /// <param name="line">Whether a decorative line exhausts the remaining command budget.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Aggregate_artwork_budget_is_enforced(bool line)
        {
            var encoded = Stencil("<shape><foreground><path><move x='0' y='0'/>" +
                string.Concat(Enumerable.Repeat("<line x='1' y='1'/>", 2000)) + "</path><fillstroke/></foreground></shape>");
            var cells = Enumerable.Range(0, line ? 49 : 51).Select(i => new XElement("mxCell",
                new XAttribute("id", "stroke-" + i), new XAttribute("parent", "1"), new XAttribute("vertex", "1"),
                new XAttribute("style", "shape=stencil(" + encoded + ");"),
                new XElement("mxGeometry", new XAttribute("width", 10), new XAttribute("height", 10)))).ToList();
            if (line)
            {
                var edge = Edge();
                edge.Element("mxGeometry")!.Element("Array")!.Add(Enumerable.Range(0, 2000).Select(_ =>
                    new XElement("mxPoint", new XAttribute("x", 1), new XAttribute("y", 1))));
                cells.Add(edge);
            }
            Assert.Contains("limit", Assert.Throws<InvalidDataException>(() => Import(SourceWith(cells.ToArray()))).Message, StringComparison.Ordinal);
        }

        /// <summary>Ordinary edits preserve paint order and clip artwork while binding geometry stays strict.</summary>
        [Fact]
        public void Cover_shapes_lines_and_stencils_preserve_geometry_and_order()
        {
            var source = SourceWith(new XElement("mxCell", new XAttribute("id", "cover"), new XAttribute("parent", "1"),
                new XAttribute("vertex", "1"), new XAttribute("style", "fillColor=#ffffff;strokeColor=none;"),
                new XElement("mxGeometry", new XAttribute("x", -2), new XAttribute("y", 20), new XAttribute("width", 30), new XAttribute("height", 10))),
                new XElement("mxCell", new XAttribute("id", "stroke"), new XAttribute("parent", "1"), new XAttribute("vertex", "1"),
                    new XAttribute("style", "shape=stencil(" + Stencil("<shape><foreground><path><move x='0' y='0'/><line x='100' y='100'/><close/></path><fillstroke/></foreground></shape>") + ");strokeColor=light-dark(transparent,#FFFFFF);"),
                    new XElement("mxGeometry", new XAttribute("x", 10), new XAttribute("y", 20), new XAttribute("width", 30), new XAttribute("height", 10))),
                Edge());
            var revision = Import(source);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var svg = XDocument.Parse(revision.Svg);
            Assert.Contains(svg.Descendants(ns + "path"), x => x.Attribute("d")!.Value == "M 10 20 L 40 30 Z");
            var stroke = svg.Descendants(ns + "g").Single(x => x.Attribute("id")?.Value == "stroke");
            Assert.Equal("#FFFFFF", stroke.Attribute("stroke")!.Value);
            var identifiers = svg.Descendants().Attributes("id").Select(x => x.Value).ToArray();
            Assert.True(Array.IndexOf(identifiers, "cover") < Array.IndexOf(identifiers, "stroke"));
            Assert.True(Array.IndexOf(identifiers, "stroke") < Array.IndexOf(identifiers, "line"));
            Assert.DoesNotContain("data-ipia-source-cell", revision.Svg, StringComparison.Ordinal);
            var labelOutside = source.Replace("x=\"10\" y=\"100\"", "x=\"9999\" y=\"100\"", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => Import(labelOutside));
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(source, GenericHardwareFixture.Definition(), "edited.drawio", "test", exporterVersion: "ipia-drawio-2"));
        }

        /// <summary>Malformed artwork is rejected before publication without broadening the executable-content contract.</summary>
        /// <param name="failure">Malformed stencil, geometry or line condition.</param>
        [Theory]
        [InlineData("tag")][InlineData("attribute")][InlineData("namespace")][InlineData("nesting")]
        [InlineData("root")][InlineData("empty")][InlineData("missing-coordinate")][InlineData("missing-y")][InlineData("negative-coordinate")][InlineData("negative-y")]
        [InlineData("nonfinite")][InlineData("big-coordinate")][InlineData("big-y")][InlineData("inflate")][InlineData("uri")][InlineData("dtd")][InlineData("encoded-limit")]
        [InlineData("color")][InlineData("width")][InlineData("text")][InlineData("transform")][InlineData("bounds")]
        [InlineData("arrow")][InlineData("curved")][InlineData("rounded")][InlineData("edge-style")][InlineData("edge-label")]
        [InlineData("edge-parent")][InlineData("endpoint")][InlineData("endpoint-coordinate")][InlineData("port")][InlineData("reference")]
        public void Rejects_invalid_artwork(string failure)
        {
            var xml = "<shape strokewidth='inherit'><foreground><path><move x='0' y='0'/><line x='100' y='100'/></path><fillstroke/></foreground></shape>";
            xml = failure switch
            {
                "tag" => xml.Replace("line", "script", StringComparison.Ordinal),
                "attribute" => xml.Replace("<move", "<move onclick='alert'", StringComparison.Ordinal),
                "namespace" => xml.Replace("<shape", "<shape xmlns='urn:unknown'", StringComparison.Ordinal),
                "nesting" => "<shape><foreground><move x='0' y='0'/></foreground></shape>",
                "root" => "<other><foreground/></other>",
                "empty" => "<shape><foreground/></shape>",
                "missing-coordinate" => xml.Replace(" x='0'", "", StringComparison.Ordinal),
                "missing-y" => xml.Replace(" y='0'", "", StringComparison.Ordinal),
                "negative-coordinate" => xml.Replace("x='0'", "x='-1'", StringComparison.Ordinal),
                "negative-y" => xml.Replace("y='0'", "y='-1'", StringComparison.Ordinal),
                "nonfinite" => xml.Replace("x='0'", "x='NaN'", StringComparison.Ordinal),
                "big-coordinate" => xml.Replace("x='0'", "x='101'", StringComparison.Ordinal),
                "big-y" => xml.Replace("y='0'", "y='101'", StringComparison.Ordinal),
                "inflate" => "<shape>" + new string(' ', 1_000_001) + "</shape>",
                "uri" => "%ZZ",
                "dtd" => "<!DOCTYPE shape [<!ENTITY value 'x'>]>" + xml,
                _ => xml
            };
            var vertex = new XElement("mxCell", new XAttribute("id", "freehand"), new XAttribute("parent", "1"), new XAttribute("vertex", "1"),
                new XAttribute("style", "shape=stencil(" + Stencil(xml, uriEncode: failure != "uri") + ");strokeColor=#ffffff;"),
                new XElement("mxGeometry", new XAttribute("x", 10), new XAttribute("y", 20), new XAttribute("width", 30), new XAttribute("height", 10)));
            var edge = Edge();
            if (failure == "encoded-limit") vertex.SetAttributeValue("style", "shape=stencil(" + new string('A', 1_000_001) + ");");
            if (failure == "color") vertex.SetAttributeValue("style", vertex.Attribute("style")!.Value + "fillColor=red;");
            if (failure == "width") vertex.SetAttributeValue("style", vertex.Attribute("style")!.Value + "strokeWidth=-1;");
            if (failure == "text") vertex.SetAttributeValue("value", "unexpected text");
            if (failure == "transform") vertex.SetAttributeValue("style", vertex.Attribute("style")!.Value + "rotation=30;");
            if (failure == "bounds") vertex.Element("mxGeometry")!.SetAttributeValue("x", 5000);
            var lineStyles = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["arrow"] = "endArrow=classic;", ["curved"] = "curved=1;", ["rounded"] = "rounded=1;", ["edge-style"] = "edgeStyle=elbowEdgeStyle;"
            };
            if (lineStyles.TryGetValue(failure, out var style)) edge.SetAttributeValue("style", style);
            if (failure == "edge-label") edge.SetAttributeValue("value", "unexpected label");
            if (failure == "edge-parent") edge.SetAttributeValue("parent", "canvas");
            if (failure == "endpoint") edge.Element("mxGeometry")!.Elements("mxPoint").First().Remove();
            if (failure == "endpoint-coordinate") edge.Element("mxGeometry")!.Elements("mxPoint").First().SetAttributeValue("x", 9999);
            if (failure == "reference") edge.SetAttributeValue("source", "missing");
            if (failure == "port") { edge.SetAttributeValue("source", "freehand"); edge.SetAttributeValue("style", "exitX=NaN;"); }
            var error = Record.Exception(() => Import(SourceWith(vertex, edge)));
            Assert.True(error is InvalidDataException or System.Xml.XmlException, error?.ToString() ?? "Malformed artwork was accepted.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UserMappingRevision Import(string source) => DrawioMappingImporter.Import(source,
            GenericHardwareFixture.Definition(), "edited.drawio", "test");
        private static string SourceWith(params XElement[] cells)
        {
            var source = XDocument.Parse(DrawioMappingFixture.Source());
            source.Descendants("root").Single().Add(cells);
            return source.ToString();
        }
        private static XElement Edge() => new("mxCell", new XAttribute("id", "line"), new XAttribute("parent", "1"), new XAttribute("edge", "1"),
            new XAttribute("style", "edgeStyle=none;endArrow=none;strokeColor=#ffffff;strokeWidth=5;"),
            new XElement("mxGeometry", new XAttribute("relative", 1), new XElement("mxPoint", new XAttribute("as", "sourcePoint"), new XAttribute("x", 10), new XAttribute("y", 20)),
                new XElement("Array", new XAttribute("as", "points"), new XElement("mxPoint", new XAttribute("x", 30), new XAttribute("y", 30))),
                new XElement("mxPoint", new XAttribute("as", "targetPoint"), new XAttribute("x", 40), new XAttribute("y", 40))));
        private static string Stencil(string xml, bool uriEncode = true)
        {
            using var bytes = new MemoryStream();
            using (var compressor = new DeflateStream(bytes, CompressionLevel.SmallestSize, true))
                compressor.Write(Encoding.UTF8.GetBytes(uriEncode ? Uri.EscapeDataString(xml) : xml));
            return Convert.ToBase64String(bytes.ToArray());
        }
        #endregion END private helpers
    }
}
