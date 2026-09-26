using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace Dcs.Ipia.Rendering;

/// <summary>Renders the canonical draw.io-generated SVG, including its real embedded device image.</summary>
public static class CanonicalSvgRenderer {
 #region BEGIN Public
 /// <summary>Rasterizes the committed SVG while replacing authored placeholder labels at display time.</summary>
 /// <param name="source">Validated SVG text read from the committed DCS-Common checkout.</param>
 /// <param name="width">Canvas width from the source viewBox.</param>
 /// <param name="height">Canvas height from the source viewBox.</param>
 /// <returns>PNG pixels for the WPF canvas; source text is untouched.</returns>
 public static byte[] Render(string source,double width,double height) {
  ArgumentException.ThrowIfNullOrWhiteSpace(source);
  if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0||width>4096||height>4096)
   throw new ArgumentOutOfRangeException(nameof(width),"SVG canvas must have finite supported dimensions.");
  var document=XDocument.Parse(source);
  foreach(var label in document.Descendants().Where(x=>x.Name.LocalName=="text"&&
   (x.Attribute("id")?.Value.StartsWith("lbl-",StringComparison.Ordinal)??false)).ToArray())label.Remove();
  return RenderComplete(document.ToString(SaveOptions.DisableFormatting),width,height);
 }
 /// <summary>Rasterizes a complete SVG with its current callout labels intact.</summary>
 /// <param name="source">Complete generated page or device SVG.</param>
 /// <param name="width">Output width in pixels.</param>
 /// <param name="height">Output height in pixels.</param>
 /// <returns>The rendered PNG.</returns>
 public static byte[] RenderComplete(string source,double width,double height) {
  ArgumentException.ThrowIfNullOrWhiteSpace(source);
  if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0||width>4096||height>4096)
   throw new ArgumentOutOfRangeException(nameof(width),"SVG canvas must have finite supported dimensions.");
  using var stream=new MemoryStream(Encoding.UTF8.GetBytes(source));
  using var svg=new SKSvg();
  var picture=svg.Load(stream)??throw new InvalidDataException("Canonical SVG could not be rendered.");
  using var surface=SKSurface.Create(new SKImageInfo((int)Math.Ceiling(width),(int)Math.Ceiling(height)))??
   throw new InvalidDataException("Could not allocate SVG surface.");
  surface.Canvas.Clear(SKColors.Transparent);
  surface.Canvas.DrawPicture(picture);
  using var image=surface.Snapshot();
  using var png=image.Encode(SKEncodedImageFormat.Png,100);
  return png.ToArray();
 }
 #endregion END Public
}
