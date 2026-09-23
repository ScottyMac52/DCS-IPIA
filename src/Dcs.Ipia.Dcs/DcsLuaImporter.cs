using System.Globalization;
using System.Text.RegularExpressions;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Dcs;

/// <summary>Imports DCS Lua source into module configurations.</summary>
public sealed class DcsLuaImporter:IModuleConfigurationImporter {
 #region BEGIN Public
 /// <summary>Imports the requested configuration.</summary>
 /// <param name="request">The request.</param>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 public Task<ModuleImportResult> ImportAsync(ModuleImportRequest request,CancellationToken ct=default) {
  ArgumentNullException.ThrowIfNull(request); ct.ThrowIfCancellationRequested(); var diagnostics=new List<ImportDiagnostic>(); var profiles=new List<DeviceProfile>(); var bindings=new List<DcsBinding>();
  IReadOnlyList<DcsModifier> modifiers=[];
  try { modifiers=string.IsNullOrWhiteSpace(request.ModifiersContent)?[]:DcsLuaParser.ParseModifiers(request.ModifiersContent); }
  catch(FormatException ex) { diagnostics.Add(new("invalid-modifiers",ex.Message,true)); }
  foreach(var source in request.Profiles) {
   try { var id=StableIdentity.Create(request.Module.DcsId,$"{source.StableDeviceId}\0{source.Instance}");profiles.Add(new(id,source.StableDeviceId,source.Instance,source.Role,source.SourceName));bindings.AddRange(DcsLuaParser.ParseProfile(source.Content,id)); }
   catch(FormatException ex) { diagnostics.Add(new("invalid-profile",$"{source.SourceName}: {ex.Message}",true)); }
  }
  if(diagnostics.Any(x=>x.IsError)) return Task.FromResult(new ModuleImportResult(null,diagnostics));
  var configuration=new ModuleConfiguration(request.Module,profiles,modifiers,bindings,request.Provenance); diagnostics.Add(new("imported",$"Imported {profiles.Count} profiles and {bindings.Count} bindings.",false)); return Task.FromResult(new ModuleImportResult(configuration,diagnostics));
 }
 #endregion END Public
}
