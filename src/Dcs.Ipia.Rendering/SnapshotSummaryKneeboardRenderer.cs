using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using SkiaSharp;

namespace Dcs.Ipia.Rendering;

/// <summary>Renders authored summary pages from a pinned workspace manifest.</summary>
public static class SnapshotSummaryKneeboardRenderer {
 #region BEGIN Public
 /// <summary>Generates every overview PNG without reading imported images or a source checkout.</summary>
 /// <param name="workspace">Frozen saved module workspace.</param>
 /// <returns>Package-relative PNG paths and bytes in ordinal order.</returns>
 public static IReadOnlyDictionary<string,byte[]> Generate(EditorWorkspaceSnapshot workspace) {
  ArgumentNullException.ThrowIfNull(workspace);
  if(workspace.Sources?.TryGetValue("config/kneeboard.json",out var source)!=true)
   throw new InvalidDataException("A pinned authored kneeboard manifest is required.");
  using var document=JsonDocument.Parse(source);
  if(!document.RootElement.TryGetProperty("summaryPages",out var summaries)||summaries.ValueKind!=JsonValueKind.Array)
   throw new InvalidDataException("The authored summary page mapping is missing.");
  var pages=workspace.Pages.Where(x=>x.Kind==KneeboardPageKind.Summary).ToArray();
  if(summaries.GetArrayLength()!=pages.Length)
   throw new InvalidDataException("Summary pages differ from the pinned manifest.");
  var results=new SortedDictionary<string,byte[]>(StringComparer.Ordinal);
  foreach(var summary in summaries.EnumerateArray()) {
   var key=summary.GetProperty("file").GetString()??"";
   if(key.Length==0||!key.All(x=>char.IsAsciiLetterOrDigit(x)||x is '-' or '_')||
      pages.Count(x=>x.StableKey==key&&x.ModuleId==workspace.Configuration.Module.Id)!=1)
    throw new InvalidDataException("Missing or unsafe authored summary page: "+key);
   if(!summary.TryGetProperty("items",out var items)||items.ValueKind!=JsonValueKind.Array||
      items.GetArrayLength() is < 1 or > 8)
    throw new InvalidDataException("The summary requires one to eight authored device rows.");
   var path=$"KNEEBOARD/{workspace.Configuration.Module.DcsId}/{key}.png";
   if(!results.TryAdd(path,Render(summary,items)))
    throw new InvalidDataException("Duplicate summary page: "+key);
  }
  return results;
 }
 #endregion END Public
 #region BEGIN private helpers
 private static byte[] Render(JsonElement summary,JsonElement items) {
  using var stream=typeof(CompositeKneeboardRenderer).Assembly.GetManifestResourceStream(
   "Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.ttf")??
   throw new InvalidDataException("The pinned kneeboard font is missing.");
  using var typeface=SKTypeface.FromStream(stream)??throw new InvalidDataException("The pinned kneeboard font is invalid.");
  using var surface=SKSurface.Create(new SKImageInfo(1200,1600))??
   throw new InvalidDataException("Could not allocate kneeboard surface.");
  var canvas=surface.Canvas;
  canvas.Clear(SKColor.Parse("#06101d"));
  using(var stripe=new SKPaint {Color=SKColor.Parse("#46d8ff")})
   canvas.DrawRect(0,0,1200,16,stripe);
  Write(summary.GetProperty("title").GetString()??"",54,82,42,SKColors.White);
  Write(summary.GetProperty("kicker").GetString()??"",56,130,19,SKColor.Parse("#ffc95c"));
  var index=0;
  foreach(var item in items.EnumerateArray()) {
   var top=205+index++*Math.Min(230,1180/items.GetArrayLength());
   var accent=item.GetProperty("accent").GetString() switch {
    "red"=>"#dc2626","blue"=>"#2563eb",_=>throw new InvalidDataException("Unsupported summary accent.")
   };
   using(var bar=new SKPaint {Color=SKColor.Parse(accent)})
    canvas.DrawRoundRect(new SKRect(54,top,66,top+145),5,5,bar);
   Write(item.GetProperty("key").GetString()??"",90,top+45,32,SKColors.White);
   Write(item.GetProperty("text").GetString()??"",90,top+96,20,SKColor.Parse("#e2e8f0"));
  }
  using var image=surface.Snapshot();
  using var png=image.Encode(SKEncodedImageFormat.Png,100);
  return png.ToArray();
  void Write(string value,float x,float y,float size,SKColor color) {
   using var font=new SKFont(typeface,size);
   if(value.Length==0||font.GetGlyphs(value).Any(glyph=>glyph==0)||font.MeasureText(value)>1040)
    throw new InvalidDataException("Summary text is empty, unsupported, or too wide: "+value);
   using var paint=new SKPaint {IsAntialias=true,Color=color};
   canvas.DrawText(value,x,y,SKTextAlign.Left,font,paint);
  }
 }
 #endregion END private helpers
}
