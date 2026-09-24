using Dcs.Ipia.Application;

namespace DcsIpia.Presentation;

/// <summary>Formats one validated Huey import for a read-only workspace window.</summary>
public sealed class HueyImportViewModel {
 /// <summary>Builds visible source and domain summaries without parsing files in the UI.</summary>
 /// <param name="result">The completed import returned by the application boundary.</param>
 public HueyImportViewModel(HueyImportResult result) {
  ArgumentNullException.ThrowIfNull(result);
  if(!result.Succeeded)throw new ArgumentException("Only validated imports can be previewed.",nameof(result));
  var configuration=result.Configuration!;
  Summary=$"{configuration.Module.DisplayName} · {configuration.Profiles.Count} profiles · {configuration.Bindings.Count} bindings · {result.Pages.Count} pages";
  Commit=$"Huey commit: {result.Provenance[0].Commit}";
  SourceNote=$"{result.Provenance.Count} committed source files · reading HEAD, so uncommitted changes are excluded";
  Profiles=configuration.Profiles.Select(x=>$"{x.DeviceId}  ·  {x.Instance??"single instance"}  ·  {configuration.Bindings.Count(y=>y.ProfileId==x.Id)} bindings  ·  {x.SourceName}").ToArray();
  Bindings=configuration.Bindings.Select(x=>{
   var profile=configuration.Profiles.Single(y=>y.Id==x.ProfileId);
   return $"{profile.DeviceId} {profile.Instance}  ·  {x.InputKey}  ·  {x.CommandName}  ·  {x.Disposition}{(x.Reformers.Count==0?"":$"  ·  {string.Join(" + ",x.Reformers)}")}";
  }).ToArray();
  Pages=result.Pages.Select(x=>$"{x.Order:00}  ·  {x.Title}  ·  {x.StableKey}").ToArray();
  UiUsage=result.UiFunctionKeys.Select(x=>$"{x}  ·  used by this module").ToArray();
  Diagnostics=result.Diagnostics.Count==0?["No import diagnostics."]:result.Diagnostics.Select(x=>$"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}").ToArray();
  Provenance=result.Provenance.Select(x=>$"{x.Path}  ·  SHA-256 {x.Sha256}").ToArray();
 }
 #region BEGIN Public
 /// <summary>Gets the module and import counts.</summary>
 public string Summary {get;}
 /// <summary>Gets the source commit description.</summary>
 public string Commit {get;}
 /// <summary>Gets the committed-source behavior reminder.</summary>
 public string SourceNote {get;}
 /// <summary>Gets the device profile rows.</summary>
 public IReadOnlyList<string> Profiles {get;}
 /// <summary>Gets the aircraft binding rows.</summary>
 public IReadOnlyList<string> Bindings {get;}
 /// <summary>Gets the kneeboard page rows.</summary>
 public IReadOnlyList<string> Pages {get;}
 /// <summary>Gets the distinct module UI Layer function rows.</summary>
 public IReadOnlyList<string> UiUsage {get;}
 /// <summary>Gets import diagnostics.</summary>
 public IReadOnlyList<string> Diagnostics {get;}
 /// <summary>Gets exact path and content hash rows.</summary>
 public IReadOnlyList<string> Provenance {get;}
 #endregion END Public
}
