using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Rendering;
using SkiaSharp;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks a single final-style device image with live labels and modifier legend.</summary>
    public sealed class CompositeKneeboardRendererTests
    {
        #region BEGIN Public
        /// <summary>Long multi-scope labels fit their authored callout in both dimensions without dropping text.</summary>
        [Fact]
        public void FitsMappedCalloutWithoutLosingVariants()
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg'><text id='lbl-one' x='50' y='50' text-anchor='middle'>Old</text></svg>",
                Controls = [new("one", "Button", "JOY_BTN1", 20, 40, 60, 20)]
            };
            var labels = new[] { "Base command", "Very long shifted UI command", "Another shifted command" };
            var page = new KneeboardComposite("Device", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["one"] = labels.Select(x => new KneeboardLabelVariant(x, "#0891b2", AssignmentLayer.UiLayer, "SHIFT")).ToArray()
            }, new Dictionary<string, string>(), []);
            var svg = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            var text = Assert.Single(svg.Descendants(), x => x.Attribute("id")?.Value == "lbl-one");
            Assert.Equal(labels, text.Elements().GroupBy(x => x.Attribute("data-full-label")!.Value)
                .Select(x => string.Join(" ", x.Select(y => y.Value))));
            using var stream = typeof(CompositeKneeboardRenderer).Assembly.GetManifestResourceStream("Heimdall.Rendering.Fonts.DejaVuSans-HEIMDALL.ttf");
            using var typeface = SKTypeface.FromStream(stream!);
            var y = float.Parse(text.Attribute("y")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            foreach (var span in text.Elements())
            {
                using var font = new SKFont(typeface, float.Parse(span.Attribute("font-size")!.Value, System.Globalization.CultureInfo.InvariantCulture));
                Assert.True(font.MeasureText(span.Value) <= 56.01f);
                y += float.Parse(span.Attribute("dy")!.Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(y + font.Metrics.Ascent, 40, 60);
                Assert.InRange(y + font.Metrics.Descent, 40, 60);
            }
        }

        /// <summary>Category text uses free space in its authored box rather than overlapping shifted controls.</summary>
        [Fact]
        public void SeparatesCategoryFromShiftedCallout()
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg'><text id='lbl-one' x='65' y='50' text-anchor='middle'>Old</text><rect x='20' y='40' width='60' height='20'/><text id='meta-category' x='50' y='50' text-anchor='middle'>Old</text></svg>",
                Controls = [new("one", "Button", "JOY_BTN1", 50, 40, 30, 20)]
            };
            var page = new KneeboardComposite("Device", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["one"] = [new("UI command", "#0891b2", AssignmentLayer.UiLayer, "SHIFT")]
            }, new Dictionary<string, string> { ["category"] = "Category" }, []);
            var svg = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            var label = Assert.Single(svg.Descendants(), x => x.Attribute("id")?.Value == "meta-category");
            Assert.True(float.Parse(label.Attribute("x")!.Value, System.Globalization.CultureInfo.InvariantCulture) < 48);
            Assert.Equal("Category", label.Value);
        }
        /// <summary>Impossible text and fully covered categories fail with actionable layout diagnostics.</summary>
        [Fact]
        public void RejectsImpossibleTextAndCategoryLayouts()
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg'><text id='lbl-one' x='50' y='50'>Old</text><rect x='20' y='40' width='60' height='20'/><text id='meta-category' x='50' y='50'>Old</text></svg>",
                Controls = [new("one", "Button", "JOY_BTN1", 20, 40, 60, 20)]
            };
            var labels = new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["one"] = [new("Command", "#ffffff", AssignmentLayer.Module, "")]
            };
            var page = new KneeboardComposite("Device", "", labels, new Dictionary<string, string> { ["category"] = "Category" }, []);
            Assert.Contains("Category has no space", Assert.Throws<InvalidDataException>(() => CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page)).Message);
            labels["one"] = [new(new string('W', 5000), "#ffffff", AssignmentLayer.Module, "")];
            Assert.Contains("Control label is too long", Assert.Throws<InvalidDataException>(() => CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page)).Message);
        }
        /// <summary>Unusable authored callout bounds fail before producing an unreadable page.</summary>
        [Theory]
        [InlineData(4, 20)]
        [InlineData(60, 2)]
        [InlineData(double.NaN, 20)]
        [InlineData(60, double.PositiveInfinity)]
        public void RejectsUnusableCalloutBounds(double width, double height)
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg'><text id='lbl-one' x='50' y='50'>Old</text></svg>",
                Controls = [new("one", "Button", "JOY_BTN1", 20, 40, width, height)]
            };
            var page = new KneeboardComposite("Device", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["one"] = [new("Command", "#ffffff", AssignmentLayer.Module, "")]
            }, new Dictionary<string, string>(), []);
            Assert.Throws<InvalidDataException>(() => CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
        }
        /// <summary>Truncated imported labels remain renderable with the pinned font.</summary>
        [Fact]
        public void RendersLongImportedControlLabels()
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><text id='lbl-rocker' x='50' y='50'>Old</text></svg>"
            };
            var page = new KneeboardComposite("Device", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["rocker"] = [new("Rocker Switch - Down - Camera Pan Down", "#ffffff", AssignmentLayer.Module, "")]
            }, new Dictionary<string, string>(), []);
            var image = CompositeKneeboardRenderer.Render(diagram, page);
            using var bitmap = SKBitmap.Decode(image);
            Assert.NotNull(bitmap);
            Assert.DoesNotContain("…", CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
        }

        /// <summary>Activator labels survive rendering and descriptive legends fit the page in both SVG and PNG output.</summary>
        [Fact]
        public void RendersActivatorLabelsAndWideLegend()
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-selector' x='50' y='50' text-anchor='middle'>Old</text></svg>"
            };
            var description = "ARM_SELECT_1 — Button 55 — PROGRAM BIT [JOY_BTN55, Toggle]";
            var page = new KneeboardComposite("Device", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["selector"] = [new("ARM_SELECT_1 (Toggle)", "#dc2626", AssignmentLayer.Module, "")]
            }, new Dictionary<string, string>(), [new(description, "#dc2626"), new(new string('W', 200), "#ea580c")]);
            var device = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            Assert.Equal("ARM_SELECT_1(Toggle)", Assert.Single(device.Descendants(), x => x.Attribute("id")?.Value == "lbl-selector").Value);
            var svg = XDocument.Parse(CompositeKneeboardRenderer.BuildPageSvg(diagram, page));
            var legend = Assert.Single(svg.Descendants(), x => x.Attribute("data-modifier-legend")?.Value == "true");
            Assert.Equal("1092", legend.Elements().First(x => x.Name.LocalName == "rect").Attribute("width")?.Value);
            Assert.Contains(legend.Elements(), x => x.Value == description);
            var longText = legend.Elements().Single(x => x.Value == new string('W', 200));
            Assert.True(float.Parse(longText.Attribute("font-size")!.Value, System.Globalization.CultureInfo.InvariantCulture) < 16);
            using var png = SKBitmap.Decode(CompositeKneeboardRenderer.Render(diagram, page, ""));
            Assert.Equal(1200, png.Width);
            Assert.Equal(1600, png.Height);
            Assert.NotEqual(SKColor.Parse("#06101d"), png.GetPixel(1100, 1360));
        }
        /// <summary>One page contains Base, shifted, and UI Layer variants plus the DCS-Common-style legend.</summary>
        [Fact]
        public void Combines_all_scope_labels_on_one_png()
        {
            const string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='50' y='50' fill='white'>Old</text><text id='meta-mfd-category-top' x='50' y='15'>Old</text></svg>";
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) { Svg = svg };
            var page = new KneeboardComposite(
                "Authored title", "CURRENT DRAFT",
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
                {
                    ["one"] =
                                                                                   [
                                                                                       new("Base label", "#ffffff",
                                                                                           AssignmentLayer.Module, ""),
                                                                                       new("Shift label", "#dc2626",
                                                                                           AssignmentLayer.Module,
                                                                                           "SHIFT"),
                                                                                       new("UI label", "#0891b2",
                                                                                           AssignmentLayer.UiLayer, "")
                                                                                   ]
                },
                new Dictionary<string, string> { ["mfd-category-top"] = "Flight" },
                [new("Base (no modifier)", "#ffffff"), new("SHIFT", "#dc2626"), new("UI Layer — Base", "#0891b2")]);
            var device = CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page);
            var document = XDocument.Parse(device);
            var label = Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "lbl-one");
            Assert.Equal(["Base label", "Shift label", "UI label"], label.Elements().Select(x => x.Value));
            Assert.Equal(
                "Flight",
                Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "meta-mfd-category-top").Value);
            var outer = CompositeKneeboardRenderer.BuildPageSvg(diagram, page);
            Assert.Contains("data-modifier-legend", outer, StringComparison.Ordinal);
            Assert.Contains("UI Layer — Base", outer, StringComparison.Ordinal);
            Assert.Contains("data:image/png;base64,", outer, StringComparison.Ordinal);
            var png = CompositeKneeboardRenderer.Render(diagram, page);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
            Assert.Equal(png, CompositeKneeboardRenderer.Render(diagram, page));
            var changed = page with { Title = "Different authored title" };
            Assert.NotEqual(png, CompositeKneeboardRenderer.Render(diagram, changed));
        }
        /// <summary>Final-style labels wrap without losing the authored base color; modifier entries stay on one callout.</summary>
        [Fact]
        public void Keeps_native_base_color_and_wraps_long_single_labels()
        {
            const string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='white'/><text id='lbl-one' x='50' y='50' fill='black'>Old</text></svg>";
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) { Svg = svg };
            var page = new KneeboardComposite(
                "Device", "",
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
                {
                    ["one"] = [new(
                                                                                   "Long function name for a control",
                                                                                   "#ffffff", AssignmentLayer.Module,
                                                                                   "")]
                },
                new Dictionary<string, string>(), []);
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            var label = Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "lbl-one");
            Assert.Equal("black", label.Attribute("fill")?.Value);
            Assert.Equal(2, label.Elements().Count());
            Assert.DoesNotContain("data-modifier-legend", CompositeKneeboardRenderer.BuildPageSvg(diagram, page));
        }
        /// <summary>The PNG respects the hardware SVG's centered callout anchor and wraps its two lines.</summary>
        [Fact]
        public void PaintsCenteredAuthoredCalloutInsteadOfForcingLeftAlignment()
        {
            const string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='50' y='50' text-anchor='middle' fill='white'>Old</text></svg>";
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) { Svg = svg };
            var page = new KneeboardComposite(
                "Device", "",
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
                {
                    ["one"] = [new("MMMM", "#ffffff", AssignmentLayer.Module, "")]
                }, new Dictionary<string, string>(), []);
            using var bitmap = SKBitmap.Decode(CompositeKneeboardRenderer.Render(diagram, page, ""));
            static bool HasText(SKBitmap image, int start, int end)
            {
                for (var y = 650; y < 830; y++)
                    for (var x = start; x < end; x++)
                    {
                        var color = image.GetPixel(x, y);
                        if (color.Red > 180 && color.Green > 180 && color.Blue > 180)
                            return true;
                    }
                return false;
            }
            Assert.True(HasText(bitmap, 440, 590));
            Assert.True(HasText(bitmap, 610, 760));
        }
        /// <summary>Right anchored and wrapped callouts retain their authored SVG position in the PNG.</summary>
        [Fact]
        public void PaintsRightAnchoredWrappedCalloutAndCenteredCategory()
        {
            const string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='50' y='50' text-anchor='end' fill='white'>Old</text><text id='meta-category' x='50' y='20' text-anchor='middle' fill='white'>Old</text></svg>";
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) { Svg = svg };
            var page = new KneeboardComposite(
                "Device", "",
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
                {
                    ["one"] = [new("Long function name for a control", "#ffffff", AssignmentLayer.Module, "")]
                }, new Dictionary<string, string> { ["category"] = "LONG CATEGORY TITLE HERE" }, []);
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            var label = Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "lbl-one");
            Assert.Equal("end", label.Attribute("text-anchor")?.Value);
            Assert.Equal(2, label.Elements().Count());
            var category = Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "meta-category");
            Assert.Equal("middle", category.Attribute("text-anchor")?.Value);
            Assert.Equal(2, category.Elements().Count());
            using var bitmap = SKBitmap.Decode(CompositeKneeboardRenderer.Render(diagram, page, ""));
            Assert.Equal(1200, bitmap.Width);
            Assert.Equal(1600, bitmap.Height);
        }
        /// <summary>Unassigned callouts stay blank and a single UI Layer label keeps its scope color.</summary>
        [Fact]
        public void Clears_unassigned_callouts_and_colors_single_ui_label()
        {
            const string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><text id='lbl-empty' x='20' y='20'>Old</text><text id='lbl-ui' x='40' y='40'>Old</text><text id='meta-category' x='50' y='50'>Old</text></svg>";
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) { Svg = svg };
            var page = new KneeboardComposite(
                "Device", "",
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
                {
                    ["ui"] =
                                                                                   [new("VR Zoom", "#0891b2",
                                                                                        AssignmentLayer.UiLayer, "")]
                },
                new Dictionary<string, string>(), []);
            var document = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            Assert.Equal("", Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "lbl-empty").Value);
            var ui = Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "lbl-ui");
            Assert.Equal("VR Zoom", ui.Value);
            Assert.Equal("#0891b2", ui.Attribute("fill")?.Value);
            Assert.Equal("",
                         Assert.Single(document.Descendants(), x => x.Attribute("id")?.Value == "meta-category").Value);
        }
        #endregion END Public
    }
}
