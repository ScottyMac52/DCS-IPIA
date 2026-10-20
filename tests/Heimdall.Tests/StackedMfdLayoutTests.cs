using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Rendering;
using Heimdall.Domain;
using SkiaSharp;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks opt-in MFD text directions, colored layers and saved exporter compatibility.</summary>
    public sealed class StackedMfdLayoutTests
    {
        #region BEGIN Public
        /// <summary>The supplied drawing imports every Cougar input and reproduces its saved revision.</summary>
        [Fact]
        public void ImportsCompleteMfdAndPreservesCategoryRegions()
        {
            var revision = Revision();
            Assert.Equal("ipia-drawio-3", revision.ExporterVersion);
            Assert.Equal(28, revision.MappedInputs.Count);
            Assert.Equal(28, revision.Controls.Count);
            DrawioMappingImporter.Validate(revision);
            var document = XDocument.Parse(revision.Svg);
            Assert.Equal("stacked", Text(document, "lbl-b1").Attribute("data-ipia-text-layout")?.Value);
            Assert.Equal("stacked", Text(document, "lbl-b15").Attribute("data-ipia-text-layout")?.Value);
            Assert.Equal("horizontal", Text(document, "lbl-b6").Attribute("data-ipia-text-layout")?.Value);
            Assert.Equal("LEFT", Text(document, "meta-mfd-category-left").Value);
        }
        /// <summary>Base, modifier and UI Layer text keeps its complete content and independent column color.</summary>
        [Theory]
        [InlineData("b1")]
        [InlineData("b15")]
        public void StacksColoredVariantsInAdjacentColumns(string control)
        {
            var revision = Revision();
            var diagram = Diagram(revision);
            var page = Page(control);
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            var spans = Text(document, "lbl-" + control).Elements().ToArray();
            foreach (var variant in page.Labels[control])
            {
                var column = spans.Where(x => x.Attribute("data-full-label")?.Value == variant.Text).ToArray();
                Assert.Equal(variant.Text, string.Concat(column.Select(x => x.Value)));
                Assert.Single(column.Select(x => x.Attribute("x")!.Value).Distinct());
                Assert.Equal(column.Length, column.Select(x => x.Attribute("y")!.Value).Distinct().Count());
                Assert.All(column, x => Assert.Equal(variant.Color == "#ffffff" ? "#172033" : variant.Color, x.Attribute("fill")?.Value));
            }
            Assert.Equal(3, spans.Select(x => x.Attribute("x")!.Value).Distinct().Count());
            Assert.All(spans, span => Assert.InRange(float.Parse(span.Attribute("font-size")!.Value,
                System.Globalization.CultureInfo.InvariantCulture), 16.25f, 36));
            Assert.Equal("LEFT", Text(document, "meta-mfd-category-left").Value);
            using var png = SKBitmap.Decode(CompositeKneeboardRenderer.Render(diagram, page));
            Assert.Equal(1200, png.Width);
            Assert.Equal(1600, png.Height);
        }
        /// <summary>Horizontal MFD variants retain distinct layer colors and ordinary text wrapping.</summary>
        [Fact]
        public void HorizontalVariantsKeepColorsAndText()
        {
            var page = Page("b6");
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(Diagram(Revision()), page));
            var spans = Text(document, "lbl-b6").Elements().ToArray();
            Assert.Contains(spans, x => x.Attribute("fill")?.Value == "#dc2626");
            Assert.Contains(spans, x => x.Attribute("fill")?.Value == "#0891b2");
            Assert.Contains(spans, x => x.Value.Contains("Base", StringComparison.Ordinal));
        }
        /// <summary>Unmarked imports keep version-two derived bytes; saved legacy revisions still validate.</summary>
        [Fact]
        public void UnmarkedDrawingsAndSavedExportersRetainTheirRendering()
        {
            var source = Revision().Source;
            var doc = XDocument.Parse(source);
            foreach (var cell in doc.Descendants("mxCell"))
            {
                cell.SetAttributeValue("style", string.Join(';', (cell.Attribute("style")?.Value ?? "").Split(';')
                    .Where(x => !x.StartsWith("ipia", StringComparison.Ordinal))));
            }
            source = doc.ToString();
            var old = DrawioMappingImporter.Import(source, Definition(), "mfd.drawio", "1", exporterVersion: "ipia-drawio-2");
            var current = DrawioMappingImporter.Import(source, Definition(), "mfd.drawio", "1");
            Assert.Equal("ipia-drawio-2", current.ExporterVersion);
            Assert.Equal(old.Id, current.Id);
            Assert.Equal(old.Svg, current.Svg);
            Assert.Equal(old.Controls, current.Controls);
            DrawioMappingImporter.Validate(old);
            DrawioMappingImporter.Validate(DrawioMappingImporter.Import(source, Definition(), "mfd.drawio", "1", exporterVersion: "ipia-drawio-1"));
            DrawioMappingImporter.Validate(current);
        }
        /// <summary>Applying the MFD drawing preserves other diagrams and every binding-related collection.</summary>
        [Fact]
        public void AdoptionChangesOnlySelectedPhysicalProfile()
        {
            var revision = Revision();
            var selected = Diagram(revision) with { ProfileId = Guid.NewGuid() };
            var stick = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [], [])
            {
                ProfileId = Guid.NewGuid(),
                Svg = "<svg xmlns='http://www.w3.org/2000/svg'><text id='lbl-trigger' x='50' y='50'>Trigger</text></svg>",
                Controls = [new("trigger", "Trigger", "JOY_BTN1", 20, 20, 60, 30)]
            };
            var configuration = new ModuleConfiguration(new(Guid.NewGuid(), "TEST", "Test"), [], [], [], "test");
            var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [selected, stick], [], [], [], new Dictionary<string, string>());
            var adopted = UserMappingAdoption.Apply(workspace, selected.ProfileId, revision);
            Assert.Same(stick, adopted.Diagrams.Single(x => x.ProfileId == stick.ProfileId));
            Assert.Same(workspace.Configuration, adopted.Configuration);
            Assert.Same(workspace.Assignments, adopted.Assignments);
            Assert.Same(workspace.Labels, adopted.Labels);
            Assert.Same(revision, adopted.Diagrams.Single(x => x.ProfileId == selected.ProfileId).CustomMapping);
        }
        /// <summary>Unusable stacked regions fail with an actionable diagnostic rather than spilling text.</summary>
        [Fact]
        public void RejectsStackedOverflow()
        {
            var diagram = Diagram(Revision()) with { Controls = [new("b1", "", "JOY_BTN1", 0, 0, 10, 10)] };
            var page = Page("b1");
            Assert.Contains("larger callout", Assert.Throws<InvalidDataException>(() => CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page)).Message);
        }
        /// <summary>Short assignments use the authored font instead of a fixed 16-unit cap.</summary>
        [Theory]
        [InlineData("b1")]
        [InlineData("b6")]
        public void HonorsAuthoredFontForShortAssignments(string control)
        {
            var page = new KneeboardComposite("MFD", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                [control] = [new("Gear", "#ffffff", AssignmentLayer.Module, "")]
            }, new Dictionary<string, string>(), []);
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(Diagram(Revision()), page));
            Assert.All(Text(document, "lbl-" + control).Elements(), x => Assert.Equal("36", x.Attribute("font-size")?.Value));
        }
        /// <summary>Existing MFD category keys substitute text at the new upright/horizontal regions.</summary>
        [Fact]
        public void ReplacesAllFourCategoryHeadingsAtAuthoredPositions()
        {
            var revision = Revision();
            var categories = new Dictionary<string, string>
            {
                ["mfd-category-left"] = "Flight",
                ["mfd-category-right"] = "Radio",
                ["mfd-category-top"] = "Navigation",
                ["mfd-category-bottom"] = "Weapons"
            };
            var page = new KneeboardComposite("MFD", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>(), categories, []);
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(Diagram(revision), page));
            foreach (var (key, value) in categories)
            {
                var text = Text(document, "meta-" + key);
                Assert.Equal(value, text.Value);
                var original = Text(XDocument.Parse(revision.Svg), "meta-" + key);
                Assert.Equal(double.Parse(original.Attribute("x")!.Value, System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(text.Attribute("x")!.Value, System.Globalization.CultureInfo.InvariantCulture), 3);
                Assert.All(text.Elements(), span => Assert.Equal("32", span.Attribute("font-size")?.Value));
                if (key.EndsWith("left", StringComparison.Ordinal) || key.EndsWith("right", StringComparison.Ordinal))
                    Assert.Equal(value.Length, text.Elements().Count());
            }
            using var png = SKBitmap.Decode(CompositeKneeboardRenderer.Render(Diagram(revision), page));
            Assert.NotNull(png);
        }
        /// <summary>Existing drawings ignore authored sizes unless they opt into the new layout.</summary>
        [Fact]
        public void UnmarkedLiveLabelsKeepLegacyFontSize()
        {
            var diagram = Diagram(Revision());
            var svg = XDocument.Parse(diagram.Svg);
            foreach (var text in svg.Descendants()) text.Attribute("data-ipia-text-layout")?.Remove();
            diagram = diagram with { Svg = svg.ToString() };
            var page = new KneeboardComposite("MFD", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["b6"] = [new("Gear", "#ffffff", AssignmentLayer.Module, "")]
            }, new Dictionary<string, string>(), []);
            var rendered = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            Assert.All(Text(rendered, "lbl-b6").Elements(), span => Assert.Equal("16", span.Attribute("font-size")?.Value));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UserMappingRevision Revision() => DrawioMappingImporter.Import(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CustomMappings", "Cougar-MFD-Stacked.drawio")),
            Definition(), "Cougar-MFD-Stacked.drawio", "1");
        private static HardwareDefinition Definition()
        {
            var objects = Enumerable.Range(0, 28).Select(i => new ControllerObject(
                Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)i << 8), "Button " + (i + 1))).ToArray();
            var capabilities = ControllerCapabilityMapper.Map(objects);
            return new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
        }
        private static UiLayerDiagram Diagram(UserMappingRevision revision) =>
            new("MFD", null, "mfd.svg", revision.Width, revision.Height, [], []) { Svg = revision.Svg, Controls = revision.Controls };
        private static KneeboardComposite Page(string control) => new("MFD", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
        {
            [control] = [new("Base command", "#ffffff", AssignmentLayer.Module, ""),
                new("Shift command", "#dc2626", AssignmentLayer.Module, "SHIFT"),
                new("UI command", "#0891b2", AssignmentLayer.UiLayer, "")]
        }, new Dictionary<string, string> { ["mfd-category-left"] = "LEFT" }, [new("Base", "#ffffff"), new("SHIFT", "#dc2626"), new("UI Layer", "#0891b2")]);
        private static XElement Text(XDocument document, string id) => document.Descendants().Single(x => x.Attribute("id")?.Value == id);
        #endregion END private helpers
    }
}
