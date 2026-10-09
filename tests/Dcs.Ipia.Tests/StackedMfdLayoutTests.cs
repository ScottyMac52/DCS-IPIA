using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;
using Dcs.Ipia.Domain;
using SkiaSharp;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks opt-in MFD text directions, colored layers and saved exporter compatibility.</summary>
    public sealed class StackedMfdLayoutTests
    {
        #region BEGIN Public
        /// <summary>The supplied drawing imports every Cougar input and reproduces its saved revision.</summary>
        [Fact]
        public void ImportsCompleteMfdAndPreservesStaticHeadings()
        {
            var revision = Revision();
            Assert.Equal(28, revision.MappedInputs.Count);
            Assert.Equal(28, revision.Controls.Count);
            DrawioMappingImporter.Validate(revision);
            var document = XDocument.Parse(revision.Svg);
            Assert.Equal("stacked", Text(document, "lbl-b1").Attribute("data-ipia-text-layout")?.Value);
            Assert.Equal("stacked", Text(document, "lbl-b15").Attribute("data-ipia-text-layout")?.Value);
            Assert.Equal("horizontal", Text(document, "lbl-b6").Attribute("data-ipia-text-layout")?.Value);
            Assert.Equal("LEFT", Text(document, "static-heading-left").Value);
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
            Assert.Equal("LEFT", Text(document, "static-heading-left").Value);
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
        }, new Dictionary<string, string>(), [new("Base", "#ffffff"), new("SHIFT", "#dc2626"), new("UI Layer", "#0891b2")]);
        private static XElement Text(XDocument document, string id) => document.Descendants().Single(x => x.Attribute("id")?.Value == id);
        #endregion END private helpers
    }
}
