using System.IO;
using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Verifies DCS HTML exports supply the same named module categories as saved catalog JSON.</summary>
public sealed class DcsHtmlCommandCatalogImporterTests {
 #region BEGIN Public
 /// <summary>Multiple devices share one catalog; category and key/axis type remain distinct.</summary>
 [Fact] public void Imports_named_categories_from_multiple_devices() {
  var first=Html("MFD 1",("JOY_BTN1","Autopilot &amp; Mode","Autopilot; Flight",Button),
   ("JOY_X","Collective","Flight Control",Axis));
  var second=Html("MFD 2",("JOY_BTN2","Autopilot &amp; Mode","Autopilot; Flight",Button));
  var html=DcsHtmlCommandCatalogImporter.Parse([("MFD 2.html",second),("MFD 1.html",first)]);
  Assert.Equal(2,html.Count);
  Assert.Equal("Autopilot & Mode",Assert.Single(html,x=>x.Id==Button).Name);
  Assert.Equal("Autopilot / Flight",Assert.Single(html,x=>x.Id==Button).Category);
  Assert.Equal("Flight Control",Assert.Single(html,x=>x.Id==Axis).Category);
  Assert.True(Assert.Single(html,x=>x.Id==Axis).IsAxis);
  const string json="""{"schemaVersion":1,"moduleId":"UH-1H","commands":[{"bindingKey":"d3011pnilu3011cd35vd1vpnilvu0","name":"Autopilot & Mode","categoryPath":["Autopilot","Flight"],"type":"button"},{"bindingKey":"a2001cdnil","name":"Collective","categoryPath":["Flight Control"],"type":"axis"}]}""";
  Assert.Equal(DcsCommandCatalogImporter.Parse(json,"UH-1H"),html);
 }
 /// <summary>Empty source categories are labeled explicitly instead of inferring Key or Axis.</summary>
 [Fact] public void Empty_category_is_uncategorized() {
  var html=Html("Stick",("","Action","","d1pnilunilcdnilvdnilvpnilvunil"));
  Assert.Equal("Uncategorized",Assert.Single(DcsHtmlCommandCatalogImporter.Parse([("Stick.htm",html)])).Category);
 }
 /// <summary>Rejects files without matching DCS device headings or valid rows.</summary>
 [Fact] public void Rejects_missing_files_bad_headings_and_rows() {
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([]));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("bad.txt","x")]));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html",Html("Other",("","A","General",Button)))]));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html","<h1>MFD 1</h1><tr><th>header</th></tr>")]));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html","<h1>MFD 1</h1><tr><td>One</td></tr>")]));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html",Html("MFD 1",("","", "General",Button)))]));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html",Html("MFD 1",("","Action","General","invalid")))]));
 }
 /// <summary>Duplicate keys or conflicting metadata across selected devices cannot silently win.</summary>
 [Fact] public void Rejects_duplicate_and_conflicting_command_hashes() {
  var duplicate=Html("MFD 1",("","Action","General",Button),("","Action","General",Button));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html",duplicate)]));
  var first=Html("MFD 1",("","Action","General",Button));
  var second=Html("MFD 2",("","Different","General",Button));
  Assert.Throws<InvalidDataException>(()=>DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html",first),("MFD 2.html",second)]));
 }
 #endregion END Public
 #region BEGIN private helpers
 private const string Button="d3011pnilu3011cd35vd1vpnilvu0";
 private const string Axis="a2001cdnil";
 private static string Html(string device,params (string Combo,string Name,string Category,string Hash)[] rows)=>
  "<html><h1>"+device+"</h1><table>"+string.Join("",rows.Select(row=>
   $"<tr><td>{row.Combo}</td><td>{row.Name}</td><td>{row.Category}</td><td>{row.Hash}</td></tr>"))+"</table></html>";
 #endregion END private helpers
}
