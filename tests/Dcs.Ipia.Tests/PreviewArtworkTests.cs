using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;
using SkiaSharp;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies graphical opacity and preview transparency without altering labels or exported pages.</summary>
    public sealed class PreviewArtworkTests
    {
        #region BEGIN Public
        /// <summary>Artwork pixels soften while text and clipping definitions keep their authored opacity.</summary>
        [Fact]
        public void Opacity_changes_graphics_without_changing_text_or_definitions()
        {
            const string source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><defs><clipPath id='c'><rect width='100' height='100'/></clipPath></defs><rect width='100' height='100' fill='red'/><text x='10' y='60' fill='white'>TEST</text></svg>";
            var prepared = PreviewArtwork.Prepare(source, 0.25);
            var doc = XDocument.Parse(prepared);
            Assert.Equal("svg", doc.Descendants().Single(x => x.Name.LocalName == "text").Parent!.Name.LocalName);
            Assert.Equal("clipPath", doc.Descendants().First(x => x.Name.LocalName == "rect").Parent!.Name.LocalName);
            using var image = SKBitmap.Decode(CanonicalSvgRenderer.RenderComplete(prepared, 100, 100));
            Assert.InRange(image.GetPixel(2, 2).Alpha, (byte)63, (byte)65);
            Assert.Contains(image.Pixels, p => p.Alpha == 255 && p.Red > 240 && p.Green > 240 && p.Blue > 240);
            Assert.Equal(source, PreviewArtwork.Prepare(source, 1));
            foreach (var invalid in new[] { -1d, 2, double.NaN, double.PositiveInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => PreviewArtwork.Prepare(source, invalid));
        }
        /// <summary>Preview page corners become transparent; ordinary exports retain opaque pixels and deterministic bytes.</summary>
        [Fact]
        public void Preview_transparency_does_not_change_export_defaults()
        {
            var diagram = new UiLayerDiagram("Device", null, "device.svg", 100, 100, [], [])
            { Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect width='100' height='100' fill='red'/></svg>" };
            var page = new KneeboardComposite("Device", "", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>(), new Dictionary<string, string>(), []);
            var original = CompositeKneeboardRenderer.Render(diagram, page);
            using var export = SKBitmap.Decode(original);
            using var preview = SKBitmap.Decode(CompositeKneeboardRenderer.Render(diagram, page, transparent: true, artworkOpacity: 0));
            Assert.Equal(255, export.GetPixel(2, 100).Alpha); Assert.Equal(0, preview.GetPixel(2, 100).Alpha);
            Assert.Equal(0, preview.GetPixel(600, 800).Alpha);
            Assert.Equal(original, CompositeKneeboardRenderer.Render(diagram, page));
        }
        #endregion END Public
    }
}
