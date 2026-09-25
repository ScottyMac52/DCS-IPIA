using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Verifies named DCS categories are imported from a module-scoped catalog.</summary>
public sealed class DcsCommandCatalogImporterTests {
 #region BEGIN Public
 /// <summary>Named category paths and key/axis types remain independent.</summary>
 [Fact] public void Imports_named_categories_and_assignable_commands() {
  var entries=DcsCommandCatalogImporter.Parse(Sample,"uh-1h");
  Assert.Equal(2,entries.Count);
  Assert.Equal("Autopilot / Modes",Assert.Single(entries,x=>x.Id=="dAuto").Category);
  Assert.False(Assert.Single(entries,x=>x.Id=="dAuto").IsAxis);
  Assert.Equal("Flight Controls",Assert.Single(entries,x=>x.Id=="aCollective").Category);
  Assert.True(Assert.Single(entries,x=>x.Id=="aCollective").IsAxis);
 }
 /// <summary>The selected module, schema and command IDs must match before use.</summary>
 [Fact] public void Rejects_wrong_module_schema_and_duplicates() {
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample,"F-16C"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("\"schemaVersion\":1","\"schemaVersion\":2"),"UH-1H"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("aCollective","dAuto"),"UH-1H"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("\"commands\":[","\"commands\":{} , \"ignored\":["),"UH-1H"));
 }
 /// <summary>Malformed entries are rejected and absent category paths are explicit.</summary>
 [Fact] public void Validates_fields_without_guessing_categories() {
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse("{broken","UH-1H"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("\"name\":\"Autopilot On\"","\"name\":\"\""),"UH-1H"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("\"type\":\"button\"","\"type\":\"unknown\""),"UH-1H"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("\"categoryPath\":[\"Flight Controls\"]","\"categoryPath\":null"),"UH-1H"));
  Assert.Throws<FormatException>(()=>DcsCommandCatalogImporter.Parse(Sample.Replace("\"bindingKey\":\"dAuto\",",""),"UH-1H"));
  var uncategorized=DcsCommandCatalogImporter.Parse(Sample.Replace("\"categoryPath\":[\"Autopilot\",\"Modes\"]","\"categoryPath\":[]"),"UH-1H");
  Assert.Equal("Uncategorized",Assert.Single(uncategorized,x=>x.Id=="dAuto").Category);
 }
 #endregion END Public
 #region BEGIN private helpers
 private const string Sample="""{"schemaVersion":1,"moduleId":"UH-1H","commands":[{"bindingKey":"dAuto","name":"Autopilot On","categoryPath":["Autopilot","Modes"],"type":"button"},{"bindingKey":"aCollective","name":"Collective","categoryPath":["Flight Controls"],"type":"axis"},{"bindingKey":"dHidden","name":"Hidden","categoryPath":[],"type":"switch","isAssignable":false}]}""";
 #endregion END private helpers
}
