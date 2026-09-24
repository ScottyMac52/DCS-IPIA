using Dcs.Ipia.Application;

namespace DcsIpia.Presentation;

/// <summary>Formats validated repository or Saved Games inputs for the read-only preview.</summary>
public sealed class HueyImportViewModel {
 /// <summary>Builds a committed Huey preview with existing rendered PNG assets.</summary>
 /// <param name="preview">The completed repository import and committed kneeboard images.</param>
 public HueyImportViewModel(HueyWorkspacePreview preview) {
  ArgumentNullException.ThrowIfNull(preview);
  var result=preview.Import;
  if(!result.Succeeded)throw new ArgumentException("Only validated imports can be previewed.",nameof(preview));
  var configuration=result.Configuration!;
  WindowTitle="Huey · committed source preview";
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
  UiUsage=result.UiFunctionKeys.Select(x=>$"{x}  ·  used by this module").ToArray();
  Diagnostics=result.Diagnostics.Count==0?["No import diagnostics."]:result.Diagnostics.Select(x=>$"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}").ToArray();
  Provenance=result.Provenance.Select(x=>$"{x.Path}  ·  SHA-256 {x.Sha256}").ToArray();
 }
 /// <summary>Builds a live Saved Games profile preview without inventing device IDs or kneeboard output.</summary>
 /// <param name="folder">The parsed local joystick folder.</param>
 public HueyImportViewModel(DcsFolderImportResult folder) {
  ArgumentNullException.ThrowIfNull(folder);
  if(!folder.Succeeded)throw new ArgumentException("Only validated profiles can be previewed.",nameof(folder));
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
  UiUsage=["Canonical UI Layer usage requires a Huey repository and DCS-Common catalog."];
  Diagnostics=folder.Diagnostics.Count==0?["No import diagnostics."]:folder.Diagnostics.Select(x=>$"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}").ToArray();
  Provenance=["Live local files; no repository commit or source hash was claimed."];
 }
 #region BEGIN Public
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
 /// <summary>Gets canonical UI Layer function rows or an availability explanation.</summary>
 public IReadOnlyList<string> UiUsage {get;}
 /// <summary>Gets import diagnostics.</summary>
 public IReadOnlyList<string> Diagnostics {get;}
 /// <summary>Gets source hashes or their availability explanation.</summary>
 public IReadOnlyList<string> Provenance {get;}
 #endregion END Public
}
