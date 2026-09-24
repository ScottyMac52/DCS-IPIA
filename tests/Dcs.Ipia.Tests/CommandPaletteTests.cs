using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Verifies palette ownership, provenance limits, and deterministic searching.</summary>
public sealed class CommandPaletteTests {
 #region BEGIN Public
 /// <summary>Imported bindings yield distinct module commands without leaking canonical UI Layer commands.</summary>
 [Fact] public void Module_palette_contains_only_imported_commands() {
  var profile=Guid.NewGuid();
  var bindings=new[] {
   new DcsBinding(profile,BindingSection.Key,"iCommandFoo","Trim",BindingDisposition.Added,"JOY_BTN1"),
   new DcsBinding(profile,BindingSection.Key,"iCommandFoo","Trim",BindingDisposition.Added,"JOY_BTN2"),
   new DcsBinding(profile,BindingSection.Axis,"iCommandAxis","Collective",BindingDisposition.Added,"JOY_X")
  };
  var aircraft=CommandPalette.FromBindings(bindings);
  var ui=CommandPalette.FromUiLayer([new(Guid.NewGuid(),Guid.NewGuid(),"UI_ZOOM","VR Zoom","View",false)]);
  Assert.Equal(2,aircraft.Count);
  Assert.Equal("Collective",aircraft[0].Name);
  Assert.True(aircraft[0].IsAxis);
  Assert.Single(CommandPalette.Search(aircraft,"FOO"));
  Assert.Empty(CommandPalette.Search(aircraft,"VR Zoom"));
  Assert.Single(CommandPalette.Search(ui,"view"));
  Assert.Empty(CommandPalette.Search(ui,"Trim"));
 }
 /// <summary>Exact category and free-text search combine without leaking commands from another scope.</summary>
 [Fact] public void Filters_exact_category_and_search_together() {
  var entries=CommandPalette.FromUiLayer([
   new(Guid.NewGuid(),Guid.NewGuid(),"ZOOM","VR Zoom","VR",false),
   new(Guid.NewGuid(),Guid.NewGuid(),"PAUSE","Active Pause","General",false),
   new(Guid.NewGuid(),Guid.NewGuid(),"RECENTER","Recenter VR","VR",false)
  ]);
  Assert.Equal(2,CommandPalette.Filter(entries,null,"VR").Count);
  Assert.Equal("VR Zoom",Assert.Single(CommandPalette.Filter(entries,"zoom","VR")).Name);
  Assert.Empty(CommandPalette.Filter(entries,"zoom","General"));
  Assert.Equal(3,CommandPalette.Filter(entries," ",null).Count);
 }
 /// <summary>Search keeps category and command ID semantics and does not reorder the input palette.</summary>
 [Fact] public void Search_is_case_insensitive_and_source_scoped() {
  var catalog=new[] {
   new DcsCommandDefinition(Guid.NewGuid(),Guid.NewGuid(),"ZOOM","VR Zoom","View",false),
   new DcsCommandDefinition(Guid.NewGuid(),Guid.NewGuid(),"PAUSE","Active Pause","Simulation",false)
  };
  var entries=CommandPalette.FromUiLayer(catalog);
  Assert.Equal("Active Pause",entries[0].Name);
  Assert.Equal("VR Zoom",entries[1].Name);
  Assert.Equal("VR Zoom",Assert.Single(CommandPalette.Search(entries,"zoom")).Name);
  Assert.Equal("Active Pause",Assert.Single(CommandPalette.Search(entries,"simulation")).Name);
  Assert.Equal(2,CommandPalette.Search(entries," ").Count);
 }
 #endregion END Public
}
