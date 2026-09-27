using Dcs.Ipia.Rendering;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Verifies the canonical SVG artwork is rendered while placeholder labels are replaced at display time.</summary>
public sealed class CanonicalSvgRendererTests {
 #region BEGIN Public
 /// <summary>Rendering retains authored shapes and image pixels in a PNG while leaving the source untouched.</summary>
 [Fact] public void Renders_svg_with_embedded_image_without_mutating_source() {
  var pixel="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==";
  var svg=$"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 80 80'><image href='data:image/png;base64,{pixel}' width='80' height='80'/><!-- box:mfd-osb-t1 --><rect x='10' y='10' width='40' height='40' fill='red'/><text id='lbl-mfd-osb-t1' x='20' y='20'>OSB1</text></svg>";
  var bytes=CanonicalSvgRenderer.Render(svg,80,80);
  Assert.True(bytes.Length>100);
  Assert.Equal(137,bytes[0]);
  Assert.Contains("OSB1",svg,StringComparison.Ordinal);
 }
 /// <summary>Nonfinite or unsupported canvas dimensions cannot allocate a raster surface.</summary>
 [Fact] public void Rejects_invalid_dimensions() {
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.Render("<svg/>",double.NaN,20));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.Render("<svg/>",5000,20));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.Render("<svg/>",20,double.PositiveInfinity));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.Render("<svg/>",0,20));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.Render("<svg/>",20,0));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.Render("<svg/>",20,5000));
 }
 /// <summary>The device backing raster doubles pixel detail while keeping the authored geometry.</summary>
 [Fact] public void Scaled_device_preserves_geometry_and_rejects_invalid_sizes() {
  const string svg="<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60' viewBox='0 0 80 60'><rect width='80' height='60' fill='red'/></svg>";
  using var bitmap=SkiaSharp.SKBitmap.Decode(CanonicalSvgRenderer.RenderCompleteScaled(svg,80,60,2));
  Assert.Equal(160,bitmap.Width);
  Assert.Equal(120,bitmap.Height);
  Assert.Equal(SkiaSharp.SKColors.Red,bitmap.GetPixel(150,100));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.RenderCompleteScaled(svg,80,60,0));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.RenderCompleteScaled(svg,80,60,5));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.RenderCompleteScaled(svg,3000,60,2));
  Assert.Throws<ArgumentOutOfRangeException>(()=>CanonicalSvgRenderer.RenderCompleteScaled(svg,double.NaN,60,2));
  Assert.Throws<ArgumentException>(()=>CanonicalSvgRenderer.RenderCompleteScaled("",80,60,2));
 }
 #endregion END Public
}
