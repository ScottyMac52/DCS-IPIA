using Dcs.Ipia.Application;

namespace DcsIpia.Presentation;

/// <summary>Formats validated repository or Saved Games inputs for the read-only preview.</summary>
public sealed class HueyImportViewModel {
 private readonly HueyWorkspacePreview? _preview;
 /// <summary>Builds a committed Huey preview with existing rendered PNG assets.</summary>
 /// <param name="preview">The completed repository import and committed kneeboard images.</param>
 public HueyImportViewModel(HueyWorkspacePreview preview) {
  ArgumentNullException.ThrowIfNull(preview);
  _preview=preview;
  var result=preview.Import;
  if(!result.Succeeded)throw new ArgumentException("Only validated imports can be previewed.",nameof(preview));
  var configuration=result.Configuration!;
  ModuleId=configuration.Module.Id;
  ModuleDcsId=configuration.Module.DcsId;
  WindowTitle="Huey · assignment editor";
  Summary=$"{configuration.Module.DisplayName} · {configuration.Profiles.Count} profiles · {configuration.Bindings.Count} bindings · {result.Pages.Count} pages";
  Commit=$"Huey commit: {result.Provenance[0].Commit}";
  SourceNote=$"{result.Provenance.Count} committed source files · {preview.Images.Count} existing kneeboard PNGs · uncommitted changes excluded";
  Profiles=configuration.Profiles.Select(x=>$"{x.DeviceId}  ·  {x.Instance??"single instance"}  ·  {configuration.Bindings.Count(y=>y.ProfileId==x.Id)} bindings  ·  {x.SourceName}").ToArray();
  Bindings=configuration.Bindings.Select(x=>{
   var profile=configuration.Profiles.Single(y=>y.Id==x.ProfileId);
   return $"{profile.DeviceId} {profile.Instance}  ·  {x.InputKey}  ·  {x.CommandName}  ·  {x.Disposition}{(x.Reformers.Count==0?"":$"  ·  {string.Join(" + ",x.Reformers)}")}";
  }).ToArray();
  Pages=result.Pages.Select(x=>$"{x.Order:00}  ·  {x.Title}").ToArray();
  PageKeys=result.Pages.Select(x=>x.StableKey).ToArray();
  Images=preview.Images;
  UiDiagrams=preview.Diagrams;
  ModifierChoices=["Base",..configuration.Modifiers.Select(x=>x.Name)];
  ModuleCommands=CommandPalette.FromBindings(configuration.Bindings);
  (Draft,DraftNote)=CreateDraft(preview,ModuleCommands);
  UiCommands=CommandPalette.FromUiLayer(preview.UiCommands);
  PaletteSourceNote="Module bindings have no category metadata. Load a DCS command catalog for named categories and all assignable commands; until then they appear as Uncategorized.";
  UiUsage=preview.Diagrams.Count==0?[preview.GraphicDiagnostics.Count>0
   ?"UI Layer graphic unavailable. See Diagnostics; imported kneeboards and profiles remain available."
   :"No graphical UI Layer assignments found in this committed source."]:[];
  var issues=result.Diagnostics.Concat(preview.GraphicDiagnostics).ToArray();
  HasErrors=issues.Any(x=>x.IsError);
  Diagnostics=issues.Length==0?["No import diagnostics."]:issues.Select(x=>$"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}").ToArray();
  Provenance=result.Provenance.Select(x=>$"{x.Path}  ·  SHA-256 {x.Sha256}").ToArray();
 }
 /// <summary>Builds a live Saved Games profile preview without inventing device IDs or kneeboard output.</summary>
 /// <param name="folder">The parsed local joystick folder.</param>
 public HueyImportViewModel(DcsFolderImportResult folder) {
  ArgumentNullException.ThrowIfNull(folder);
  if(folder.Profiles.Count==0)throw new ArgumentException("No readable profiles are available for preview.",nameof(folder));
  ModuleId=Guid.Empty;
  ModuleDcsId=string.Empty;
  WindowTitle="DCS Saved Games · joystick source preview";
  Summary=$"{folder.Profiles.Count} local profiles · {folder.Bindings.Count} bindings · {folder.Modifiers.Count} modifiers";
  Commit=$"Source folder: {folder.Folder}";
  SourceNote="Reading live Saved Games files · device identities are not inferred from filenames";
  Profiles=folder.Profiles.Select(x=>$"{x.SourceName}  ·  {folder.Bindings.Count(y=>y.ProfileId==x.Id)} bindings  ·  device not resolved").ToArray();
  Bindings=folder.Bindings.Select(x=>{
   var profile=folder.Profiles.Single(y=>y.Id==x.ProfileId);
   return $"{profile.SourceName}  ·  {x.InputKey}  ·  {x.CommandName}  ·  {x.Disposition}{(x.Reformers.Count==0?"":$"  ·  {string.Join(" + ",x.Reformers)}")}";
  }).ToArray();
  Pages=["Saved Games joystick files contain no kneeboard images. Open a Huey repository to preview its committed PNGs."];
  PageKeys=[];
  Images=[];
  UiDiagrams=[];
  Draft=null;
  ModifierChoices=["Base"];
  DraftNote="Import both committed repositories to edit an identified device.";
  ModuleCommands=CommandPalette.FromBindings(folder.Bindings);
  UiCommands=[];
  PaletteSourceNote="Saved Games bindings have no category metadata. Open both repositories, then load a DCS command catalog for named categories.";
  UiUsage=["Canonical UI Layer graphics require a Huey repository and DCS-Common catalog."];
  HasErrors=folder.Diagnostics.Any(x=>x.IsError);
  Diagnostics=folder.Diagnostics.Count==0?["No import diagnostics."]:folder.Diagnostics.Select(x=>$"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}").ToArray();
  Provenance=["Live local files; no repository commit or source hash was claimed."];
 }
 #region BEGIN Public
 /// <summary>Gets the selected imported module identity.</summary>
 public Guid ModuleId {get;}
 /// <summary>Gets the DCS aircraft identifier required to validate a command catalog.</summary>
 public string ModuleDcsId {get;}
 /// <summary>Gets the preview window title.</summary>
 public string WindowTitle {get;}
 /// <summary>Gets the module and import counts.</summary>
 public string Summary {get;}
 /// <summary>Gets the source origin.</summary>
 public string Commit {get;}
 /// <summary>Gets a reminder about committed or live source behavior.</summary>
 public string SourceNote {get;}
 /// <summary>Gets device profile rows.</summary>
 public IReadOnlyList<string> Profiles {get;}
 /// <summary>Gets aircraft binding rows.</summary>
 public IReadOnlyList<string> Bindings {get;}
 /// <summary>Gets kneeboard page labels or a missing asset explanation.</summary>
 public IReadOnlyList<string> Pages {get;}
 /// <summary>Gets page identities aligned to the displayed rows.</summary>
 public IReadOnlyList<string> PageKeys {get;}
 /// <summary>Gets existing committed kneeboard PNGs.</summary>
 public IReadOnlyList<KneeboardImageAsset> Images {get;}
 /// <summary>Gets diagrams positioned on committed shared hardware images.</summary>
 public IReadOnlyList<UiLayerDiagram> UiDiagrams {get;}
 /// <summary>Gets imported aircraft commands; this is not a complete aircraft command catalog.</summary>
 public IReadOnlyList<CommandPaletteEntry> ModuleCommands {get;private set;}
 /// <summary>Gets canonical UI Layer commands, when DCS-Common was imported.</summary>
 public IReadOnlyList<CommandPaletteEntry> UiCommands {get;}
 /// <summary>Gets command source and completeness information.</summary>
 public string PaletteSourceNote {get;private set;}
 /// <summary>Gets the in-memory draft editor when canonical controls and source identities are available.</summary>
 public DraftAssignmentService? Draft {get;private set;}
 /// <summary>Gets Base and explicit modifier names from the imported module.</summary>
 public IReadOnlyList<string> ModifierChoices {get;}
 /// <summary>Gets the editor availability or unmapped-binding diagnostic.</summary>
 public string DraftNote {get;private set;}
 /// <summary>Gets an explanation when graphics are unavailable.</summary>
 public IReadOnlyList<string> UiUsage {get;}
 /// <summary>Gets whether any source or graphic diagnostic requires attention.</summary>
 public bool HasErrors {get;}
 /// <summary>Gets import diagnostics.</summary>
 public IReadOnlyList<string> Diagnostics {get;}
 /// <summary>Gets source hashes or their availability explanation.</summary>
 public IReadOnlyList<string> Provenance {get;}
 /// <summary>Loads a validated module catalog and refreshes the draft before editing begins.</summary>
 /// <param name="catalog">Commands parsed from a DCS-Common command catalog for this module.</param>
 public void LoadModuleCatalog(IReadOnlyList<CommandPaletteEntry> catalog) {
  ArgumentNullException.ThrowIfNull(catalog);
  if(_preview is null)throw new InvalidOperationException("Import both repositories before loading a module catalog.");
  if(Draft?.HasChanges==true)throw new InvalidOperationException("Review or discard pending edits before replacing the command catalog.");
  var commands=CommandPalette.FromBindings(_preview.Import.Configuration!.Bindings,catalog);
  var (draft,note)=CreateDraft(_preview,commands);
  if(draft is null)throw new InvalidOperationException(note);
  ModuleCommands=commands;Draft=draft;DraftNote=note;
  PaletteSourceNote="Aircraft commands and named categories from the selected DCS catalog; imported bindings absent from it appear as Uncategorized.";
 }
 #endregion END Public
 #region BEGIN private helpers
 private static (DraftAssignmentService?,string) CreateDraft(HueyWorkspacePreview preview,IReadOnlyList<CommandPaletteEntry> catalog) {
  var configuration=preview.Import.Configuration!;
  if(preview.Diagrams.Count==0||preview.Diagrams.All(x=>x.Controls.Count==0))
   return (null,"No mapped controls in a validated draw.io-generated SVG; editing is unavailable.");
  try {
   var moduleCommands=catalog.Select(x=>new DraftCommand(x.Id,x.Name,x.IsAxis,AssignmentLayer.Module)).ToArray();
   var moduleNames=catalog.ToDictionary(x=>x.Id,x=>x.Name,StringComparer.Ordinal);
   var uiCommands=preview.UiCommands
    .Select(x=>new DraftCommand(x.DcsCommandId,x.Name,x.IsAxis,AssignmentLayer.UiLayer));
   var assignments=new List<DraftAssignment>();
   var unmatched=0;
   foreach(var binding in configuration.Bindings.Where(x=>x.Disposition!=global::Dcs.Ipia.Domain.BindingDisposition.Removed)) {
    var diagram=preview.Diagrams.FirstOrDefault(x=>x.ProfileId==binding.ProfileId);
    var matches=diagram?.Controls.Where(x=>x.InputKey==binding.InputKey).ToArray()??[];
    if(matches.Length!=1) {unmatched++;continue;}
    var slot=new DraftAssignmentSlot(configuration.Module.Id,binding.ProfileId,AssignmentLayer.Module,binding.Section,matches[0].InputKey,
     string.Join("+",binding.Reformers));
    assignments.Add(new(slot,new(binding.CommandId,moduleNames[binding.CommandId],binding.Section==global::Dcs.Ipia.Domain.BindingSection.Axis,AssignmentLayer.Module)));
   }
   foreach(var diagram in preview.Diagrams)foreach(var callout in diagram.Callouts) {
    var control=diagram.Controls.SingleOrDefault(x=>x.Control==callout.Control);
    if(control is null) {unmatched++;continue;}
    assignments.Add(new(new(configuration.Module.Id,diagram.ProfileId,AssignmentLayer.UiLayer,
     global::Dcs.Ipia.Domain.BindingSection.Key,control.InputKey,callout.Modifier.Replace(" + ","+",StringComparison.Ordinal)),
     new(callout.CommandId,callout.Function,false,AssignmentLayer.UiLayer)));
   }
   var editor=new DraftAssignmentService(configuration,moduleCommands.Concat(uiCommands),assignments);
   return (editor,unmatched==0?"Changes are staged in memory. Review, undo or discard them before leaving this window.":
    $"{unmatched} imported bindings could not be mapped to authored controls and are excluded from this draft. No source file is changed.");
  }
  catch(Exception ex) when(ex is ArgumentException or InvalidOperationException) {
   return (null,"Editing unavailable: "+ex.Message);
  }
 }
 #endregion END private helpers
}
