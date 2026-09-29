using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;
using SkiaSharp;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks a single final-style device image with live labels and modifier legend.</summary>
    public sealed class CompositeKneeboardRendererTests
    {
        #region BEGIN Public
        /// <summary>One page contains Base, shifted, and UI Layer variants plus the DCS-Common-style legend.</summary>
        [Fact]
        public void Combines_all_scope_labels_on_one_png()
        {
            const string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='50' y='50' fill='white'>Old</text><text id='meta-mfd-category-top' x='50' y='15'>Old</text></svg>";
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], []) { Svg = svg };
            var page = new KneeboardComposite(
                "Authored title", "CURRENT DRAFT",
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> { ["one"] =
                                                                                   [
                                                                                       new("Base label", "#ffffff",
                                                                                           AssignmentLayer.Module, ""),
                                                                                       new("Shift label", "#dc2626",
                                                                                           AssignmentLayer.Module,
                                                                                           "SHIFT"),
                                                                                       new("UI label", "#0891b2",
                                                                                           AssignmentLayer.UiLayer, "")
                                                                                   ] },
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
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> { ["one"] = [new(
                                                                                   "Long function name for a control",
                                                                                   "#ffffff", AssignmentLayer.Module,
                                                                                   "")] },
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
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> {
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
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> {
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
                new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> { ["ui"] =
                                                                                   [new("VR Zoom", "#0891b2",
                                                                                        AssignmentLayer.UiLayer, "")] },
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
