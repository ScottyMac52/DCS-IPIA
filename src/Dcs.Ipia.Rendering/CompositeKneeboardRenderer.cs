using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using SkiaSharp;

namespace Dcs.Ipia.Rendering;

/// <summary>Builds one DCS-Common-style device PNG from staged Module and UI Layer assignments.</summary>
public static class CompositeKneeboardRenderer {
 #region BEGIN Public
 /// <summary>Renders the combined device page to a 1200 by 1600 PNG without writing a repository.</summary>
 /// <param name="diagram">Stored draw.io-generated device SVG.</param>
 /// <param name="page">Current labels, authored page title and modifier legend.</param>
 /// <param name="footer">Preview footer, or empty when exporting an authored page.</param>
 /// <returns>The complete rendered PNG.</returns>
 public static byte[] Render(UiLayerDiagram diagram,KneeboardComposite page,string footer="DCS-IPIA • current draft preview") {
  var outer=CanonicalSvgRenderer.RenderComplete(BuildPageSvg(diagram,page,footer),1200,1600);
  var device=CanonicalSvgRenderer.RenderCompleteScaled(BuildDeviceSvg(diagram,page),diagram.Width,diagram.Height,
   Math.Min(2,(int)Math.Floor(4096/Math.Max(diagram.Width,diagram.Height))));
  using var pageImage=SKImage.FromEncodedData(outer);
  using var deviceImage=SKImage.FromEncodedData(device);
  using var surface=SKSurface.Create(new SKImageInfo(1200,1600))??
   throw new InvalidDataException("Could not allocate kneeboard surface.");
  var sampling=new SKSamplingOptions(SKFilterMode.Linear);
  surface.Canvas.DrawImage(pageImage,0,0,sampling);
  var height=page.Legend.Count>0?1170:1350;
  var scale=Math.Min(1130f/deviceImage.Width,height/(float)deviceImage.Height);
  var width=deviceImage.Width*scale;
  var fittedHeight=deviceImage.Height*scale;
  var bounds=new SKRect(35+(1130-width)/2,155+(height-fittedHeight)/2,
   35+(1130+width)/2,155+(height+fittedHeight)/2);
  surface.Canvas.DrawImage(deviceImage,bounds,sampling);
  PaintPageText(surface.Canvas,page,footer);
  surface.Canvas.Save();
  surface.Canvas.Translate(bounds.Left,bounds.Top);
  surface.Canvas.Scale(bounds.Width/(float)diagram.Width,bounds.Height/(float)diagram.Height);
  PaintLabels(surface.Canvas,BuildDeviceSvg(diagram,page),page);
  surface.Canvas.Restore();
  using var image=surface.Snapshot();
  using var png=image.Encode(SKEncodedImageFormat.Png,100);
  return png.ToArray();
 }
 /// <summary>Builds the final-style page SVG for inspection and deterministic tests.</summary>
 /// <param name="diagram">Stored draw.io-generated device SVG.</param>
 /// <param name="page">Current combined labels and legend.</param>
 /// <param name="footer">Preview footer, or empty to retain only the authored SVG footer.</param>
 /// <returns>One complete SVG page with device image and modifier legend.</returns>
 public static string BuildPageSvg(UiLayerDiagram diagram,KneeboardComposite page,string footer="DCS-IPIA • current draft preview") {
  ArgumentNullException.ThrowIfNull(diagram);
  ArgumentNullException.ThrowIfNull(page);
  var device=BuildDeviceSvg(diagram,page);
  var png=CanonicalSvgRenderer.RenderCompleteScaled(device,diagram.Width,diagram.Height,
   Math.Min(2,(int)Math.Floor(4096/Math.Max(diagram.Width,diagram.Height))));
  var encoded=Convert.ToBase64String(png);
  var legend=BuildLegend(page.Legend);
  var hardwareHeight=page.Legend.Count>0?1170:1350;
  return $"""
   <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1600" viewBox="0 0 1200 1600">
     <desc>Current DCS-IPIA device kneeboard: {Escape(diagram.Title)}</desc>
     <rect width="1200" height="1600" fill="#06101d"/>
     <rect width="1200" height="16" fill="#46d8ff"/>
     <text x="54" y="80" font-family="Arial,sans-serif" font-size="44" font-weight="700" fill="#f5f9ff">{Escape(page.Title)}</text>
     <text x="56" y="126" font-family="Arial,sans-serif" font-size="20" font-weight="700" fill="#ffc95c">{Escape(page.Kicker)}</text>
     <image href="data:image/png;base64,{encoded}" x="35" y="155" width="1130" height="{hardwareHeight}" preserveAspectRatio="xMidYMid meet"/>
   {legend}
    {(footer.Length==0?"":$"<text x=\"54\" y=\"1570\" font-family=\"Arial,sans-serif\" font-size=\"18\" fill=\"#8ea5bd\">{Escape(footer)}</text>")}
   </svg>
   """;
 }
 /// <summary>Substitutes every staged callout variant into the authored hardware SVG.</summary>
 /// <param name="diagram">Stored draw.io-generated device SVG.</param>
 /// <param name="page">Combined device labels.</param>
 /// <returns>Hardware SVG with current colored labels.</returns>
 public static string BuildDeviceSvg(UiLayerDiagram diagram,KneeboardComposite page) {
  ArgumentNullException.ThrowIfNull(diagram);
  ArgumentNullException.ThrowIfNull(page);
  using var reader=XmlReader.Create(new StringReader(diagram.Svg),new XmlReaderSettings {
   DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4_000_000
  });
  var root=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
  foreach(var label in root.Descendants().Where(x=>x.Name.LocalName=="text")) {
   var id=label.Attribute("id")?.Value;
   if(id?.StartsWith("lbl-",StringComparison.Ordinal)==true) {
    var control=id["lbl-".Length..];
    page.Labels.TryGetValue(control,out var variants);
    label.RemoveNodes();
    label.SetAttributeValue("font-size",variants?.Count>1?"13":"16");
    label.SetAttributeValue("font-weight","700");
    if(variants is null||variants.Count==0)continue;
    var x=label.Attribute("x")?.Value??"0";
    if(variants.Count==1) {
     var variant=variants[0];
     // DCS-Common retains the SVG's native fill for an unmodified base label.
     if(variant.Color!="#ffffff"||variant.Scope!=AssignmentLayer.Module||variant.ModifierChord.Length>0)
      label.SetAttributeValue("fill",variant.Color);
     var lines=WrapLabel(variant.Text);
     if(lines.Count==1)label.Value=lines[0];
     else for(var i=0;i<lines.Count;i++)
      label.Add(new XElement(label.Name.Namespace+"tspan",new XAttribute("x",x),
       new XAttribute("dy",i==0?-8:16),new XAttribute("font-size",16),
       new XAttribute("font-weight","700"),lines[i]));
     continue;
    }
    for(var i=0;i<variants.Count;i++) {
     var variant=variants[i];
     var span=new XElement(label.Name.Namespace+"tspan",
      new XAttribute("x",x),new XAttribute("dy",i==0?-(variants.Count-1)*8:16),
      new XAttribute("font-size",13),new XAttribute("font-weight","700"),
      new XAttribute("data-full-label",variant.Text),variant.Text);
     if(variant.Color!="#ffffff"||variant.Scope!=AssignmentLayer.Module||variant.ModifierChord.Length>0)
      span.SetAttributeValue("fill",variant.Color);
     label.Add(span);
    }
   } else if(id?.StartsWith("meta-",StringComparison.Ordinal)==true) {
    label.RemoveNodes();
    label.Value=page.CategoryLabels.GetValueOrDefault(id["meta-".Length..])??"";
   }
  }
  return root.ToString(SaveOptions.DisableFormatting);
 }
 #endregion END Public
 #region BEGIN private helpers
 private static SKTypeface LabelTypeface() {
  var source=typeof(CompositeKneeboardRenderer).Assembly.GetManifestResourceStream(
   "Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.ttf")??
   throw new InvalidDataException("The pinned kneeboard font is missing.");
  return SKTypeface.FromStream(source)??throw new InvalidDataException("The pinned kneeboard font is invalid.");
 }
 private static void PaintPageText(SKCanvas canvas,KneeboardComposite page,string footer) {
  using var typeface=LabelTypeface();
  Write(page.Title,54,80,44,SKColors.White);
  Write(page.Kicker,56,126,20,SKColor.Parse("#ffc95c"));
  if(page.Legend.Count>0) {
   Write("SHIFT / MODIFIER",66,1372,14,SKColor.Parse("#94a3b8"));
   for(var i=0;i<page.Legend.Count;i++)
    Write(page.Legend[i].Label,88,1394+i*28,16,SKColor.Parse("#e2e8f0"));
  }
  if(footer.Length>0)Write(footer,54,1570,18,SKColor.Parse("#8ea5bd"));
  void Write(string value,float x,float y,float size,SKColor color) {
   using var font=new SKFont(typeface,size);
   if(font.GetGlyphs(value).Any(x=>x==0))
    throw new InvalidDataException("Kneeboard text contains a glyph absent from the pinned font: "+value);
   using var paint=new SKPaint {IsAntialias=true,Color=color};
   canvas.DrawText(value,x,y,SKTextAlign.Left,font,paint);
  }
 }
 private static void PaintLabels(SKCanvas canvas,string svg,KneeboardComposite page) {
  var source=XDocument.Parse(svg);
  using var typeface=LabelTypeface();
  foreach(var (control,variants) in page.Labels) {
   var label=source.Descendants().FirstOrDefault(x=>x.Attribute("id")?.Value=="lbl-"+control);
   if(label is null||variants.Count==0)continue;
   if(!float.TryParse(label.Attribute("x")?.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var x)||
      !float.TryParse(label.Attribute("y")?.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var y))
    throw new InvalidDataException("Authored callout has no usable text position: "+control);
   using var font=new SKFont(typeface,variants.Count>1?13:16);
   for(var i=0;i<variants.Count;i++) {
    var variant=variants[i];
    if(font.GetGlyphs(variant.Text).Any(x=>x==0))
     throw new InvalidDataException("Callout text contains a glyph absent from the pinned font: "+variant.Text);
    var color=variant.Color=="#ffffff"&&variant.Scope==AssignmentLayer.Module&&variant.ModifierChord.Length==0?
     label.Attribute("fill")?.Value??variant.Color:variant.Color;
    using var paint=new SKPaint {IsAntialias=true,Color=SKColor.TryParse(color,out var parsed)?parsed:SKColors.White};
    canvas.DrawText(variant.Text,x,y+(i-(variants.Count-1)/2f)*16,SKTextAlign.Left,font,paint);
   }
  }
  foreach(var (name,value) in page.CategoryLabels) {
   var label=source.Descendants().FirstOrDefault(x=>x.Attribute("id")?.Value=="meta-"+name);
   if(label is null)continue;
   if(!float.TryParse(label.Attribute("x")?.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var x)||
      !float.TryParse(label.Attribute("y")?.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var y))
    throw new InvalidDataException("Authored category has no usable text position: "+name);
   using var font=new SKFont(typeface,14);
   if(font.GetGlyphs(value).Any(x=>x==0))
    throw new InvalidDataException("Category text contains a glyph absent from the pinned font: "+value);
   var fill=label.Attribute("fill")?.Value??"#ffffff";
   using var paint=new SKPaint {IsAntialias=true,Color=SKColor.TryParse(fill,out var parsed)?parsed:SKColors.White};
   canvas.DrawText(value,x,y,SKTextAlign.Left,font,paint);
  }
 }
 private static List<string> WrapLabel(string value) {
  var words=value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
  var lines=new List<string>();
  foreach(var word in words) {
   if(lines.Count==0||lines[^1].Length+1+word.Length>18)lines.Add(word);
   else lines[^1]+=" "+word;
  }
  if(lines.Count<=2)return lines.Count==0?[""]:lines;
  var tail=string.Join(" ",lines.Skip(1));
  return [lines[0],tail[..Math.Min(17,tail.Length)].TrimEnd()+"…"];
 }
 private static string BuildLegend(IReadOnlyList<KneeboardModifierLegend> legend) {
  if(legend.Count==0)return "";
  var result=new StringBuilder();
  var height=36+legend.Count*28;
  result.Append(CultureInfo.InvariantCulture,$"<g data-modifier-legend=\"true\"><rect x=\"54\" y=\"1350\" width=\"400\" height=\"{height}\" rx=\"8\" fill=\"#0b1726\" stroke=\"#334155\" stroke-width=\"1.5\"/><text x=\"66\" y=\"1372\" font-family=\"Arial,sans-serif\" font-size=\"14\" font-weight=\"700\" fill=\"#94a3b8\">SHIFT / MODIFIER</text>");
  for(var i=0;i<legend.Count;i++) {
   var y=1382+i*28;
   result.Append(CultureInfo.InvariantCulture,$"<rect x=\"66\" y=\"{y}\" width=\"14\" height=\"14\" rx=\"2\" fill=\"{Escape(legend[i].Color)}\"/>");
   result.Append(CultureInfo.InvariantCulture,$"<text x=\"88\" y=\"{y+12}\" font-family=\"Arial,sans-serif\" font-size=\"16\" fill=\"#e2e8f0\">{Escape(legend[i].Label)}</text>");
  }
  return result.Append("</g>").ToString();
 }
 private static string Escape(string value)=>SecurityElement.Escape(value)??"";
 #endregion END private helpers
}
