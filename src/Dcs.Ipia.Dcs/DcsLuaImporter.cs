using System.Globalization;
using System.Text.RegularExpressions;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs;

public sealed class DcsLuaImporter:IModuleConfigurationImporter {
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
}

public static class DcsLuaParser {
 public static IReadOnlyList<DcsBinding> ParseProfile(string source,Guid profileId) {
  ArgumentException.ThrowIfNullOrWhiteSpace(source); if(profileId==Guid.Empty)throw new ArgumentException("A profile ID is required.",nameof(profileId)); if(!ReturnDiffPattern.IsMatch(source))throw new FormatException("Profile must return the diff table.");
  var result=new List<DcsBinding>();
  foreach(var pair in new[]{(Name:"keyDiffs",Section:BindingSection.Key),(Name:"axisDiffs",Section:BindingSection.Axis)}) {
   var table=TableBody(source,pair.Name); foreach(var entry in NamedTables(table)) {var name=StringField(entry.Body,"name")??throw new FormatException($"{pair.Name}.{entry.Name} has no name.");
    AddInputs(result,profileId,pair.Section,entry.Name,name,entry.Body,"added",BindingDisposition.Added); AddInputs(result,profileId,pair.Section,entry.Name,name,entry.Body,"changed",BindingDisposition.Changed); AddInputs(result,profileId,pair.Section,entry.Name,name,entry.Body,"removed",BindingDisposition.Removed);
   }
  }
  return result;
 }
 public static IReadOnlyList<DcsModifier> ParseModifiers(string source) {
  ArgumentException.ThrowIfNullOrWhiteSpace(source); if(!ReturnModifiersPattern.IsMatch(source))throw new FormatException("Modifiers file must return the modifiers table."); var root=RootTableBody(source,"modifiers"); var result=new List<DcsModifier>();
  foreach(var entry in NamedTables(root)) {var device=StringField(entry.Body,"device");var key=StringField(entry.Body,"key");var switched=BoolField(entry.Body,"switch");if(device is null||key is null||switched is null)throw new FormatException($"Modifier {entry.Name} requires device, key, and switch.");result.Add(new(entry.Name,device,key,switched.Value?ModifierMode.Toggle:ModifierMode.Hold));}
  if(result.Select(x=>x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=result.Count)throw new FormatException("Modifier names must be unique ignoring case."); if(result.Select(x=>$"{x.Device}\0{x.Key}").Distinct(StringComparer.OrdinalIgnoreCase).Count()!=result.Count)throw new FormatException("Modifier physical inputs must be unique."); return result.OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray();
 }
 private static void AddInputs(List<DcsBinding> result,Guid profileId,BindingSection section,string command,string name,string body,string list,BindingDisposition disposition) {
  foreach(var input in IndexedTables(TableBody(body,list))) {var key=StringField(input,"key")??throw new FormatException($"{list} input has no key.");var reformers=IndexedStringValues(TableBody(input,"reformers"));var filter=section==BindingSection.Axis?ParseFilter(TableBody(input,"filter")):null;result.Add(new(profileId,section,command,name,disposition,key,reformers,filter));}
 }
 private static AxisFilter? ParseFilter(string body) {if(body.Length==0)return null;return new(NumberField(body,"deadzone"),NumberField(body,"saturationX"),NumberField(body,"saturationY"),IndexedNumberValues(TableBody(body,"curvature")),BoolField(body,"invert"),BoolField(body,"slider"),BoolField(body,"hardwareDetent"),NumberField(body,"hardwareDetentAB"),NumberField(body,"hardwareDetentMax"));}
 private static string RootTableBody(string source,string variable) {var marker=$"local {variable}";var start=source.IndexOf(marker,StringComparison.Ordinal);if(start<0)throw new FormatException($"File must declare local {variable}.");var open=source.IndexOf('{',start+marker.Length);if(open<0)throw new FormatException($"{variable} is not a Lua table.");return source[(open+1)..MatchingBrace(source,open)];}
 private static string TableBody(string source,string name) {var marker=$"[\"{name}\"]";var start=source.IndexOf(marker,StringComparison.Ordinal);if(start<0)return string.Empty;var open=source.IndexOf('{',start+marker.Length);if(open<0)throw new FormatException($"{name} is not a Lua table.");return source[(open+1)..MatchingBrace(source,open)];}
 private static List<(string Name,string Body)> NamedTables(string body) {var result=new List<(string,string)>();var index=0;while((index=body.IndexOf("[\"",index,StringComparison.Ordinal))>=0){if(Depth(body,index)!=0){index+=2;continue;}var end=FindStringEnd(body,index+2);var name=Unescape(body[(index+2)..end]);var equals=SkipWhite(body,end+2);if(equals>=body.Length||body[equals]!='='){index=end+1;continue;}var open=SkipWhite(body,equals+1);if(open>=body.Length||body[open]!='{'){index=end+1;continue;}var close=MatchingBrace(body,open);result.Add((name,body[(open+1)..close]));index=close+1;}return result;}
 private static List<string> IndexedTables(string body) {var result=new List<string>();var index=0;while((index=body.IndexOf('[',index))>=0){if(Depth(body,index)!=0){index++;continue;}var closeBracket=body.IndexOf(']',index+1);if(closeBracket<0||!int.TryParse(body.AsSpan(index+1,closeBracket-index-1),NumberStyles.None,CultureInfo.InvariantCulture,out _)){index++;continue;}var equals=SkipWhite(body,closeBracket+1);if(equals>=body.Length||body[equals]!='='){index++;continue;}var open=SkipWhite(body,equals+1);if(open>=body.Length||body[open]!='{'){index++;continue;}var close=MatchingBrace(body,open);result.Add(body[(open+1)..close]);index=close+1;}return result;}
 private static string? StringField(string body,string name) {var marker=$"[\"{name}\"]";var start=body.IndexOf(marker,StringComparison.Ordinal);if(start<0)return null;var equals=body.IndexOf('=',start+marker.Length);if(equals<0)return null;var quote=body.IndexOf('"',equals+1);if(quote<0)return null;var end=FindStringEnd(body,quote+1);return Unescape(body[(quote+1)..end]);}
 private static bool? BoolField(string body,string name) {var raw=ScalarField(body,name);return raw switch{"true"=>true,"false"=>false,_=>null};}
 private static double? NumberField(string body,string name) {var raw=ScalarField(body,name);return double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:null;}
 private static string? ScalarField(string body,string name) {var marker=$"[\"{name}\"]";var start=body.IndexOf(marker,StringComparison.Ordinal);if(start<0)return null;var equals=body.IndexOf('=',start+marker.Length);if(equals<0)return null;var begin=SkipWhite(body,equals+1);var end=begin;while(end<body.Length&&!char.IsWhiteSpace(body[end])&&body[end]!=','&&body[end]!='}')end++;return body[begin..end];}
 private static string[] IndexedStringValues(string body)=>IndexedValuePattern.Matches(body).Cast<Match>().Select(x=>Unescape(x.Groups[1].Value)).Order(StringComparer.Ordinal).ToArray();
 private static double[]? IndexedNumberValues(string body) {if(body.Length==0)return null;return IndexedNumberPattern.Matches(body).Cast<Match>().Select(x=>double.Parse(x.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();}
 private static int MatchingBrace(string source,int open) {var depth=0;var quote='\0';var escaped=false;for(var i=open;i<source.Length;i++){var c=source[i];if(quote!='\0'){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote='\0';continue;}if(c is '\"' or '\'')quote=c;else if(c=='{')depth++;else if(c=='}'&&--depth==0)return i;}throw new FormatException("Unbalanced Lua table braces.");}
 private static int Depth(string source,int end) {var depth=0;var quote='\0';var escaped=false;for(var i=0;i<end;i++){var c=source[i];if(quote!='\0'){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote='\0';}else if(c is '\"' or '\'')quote=c;else if(c=='{')depth++;else if(c=='}')depth--;}return depth;}
 private static int FindStringEnd(string value,int start) {var escaped=false;for(var i=start;i<value.Length;i++){if(escaped)escaped=false;else if(value[i]=='\\')escaped=true;else if(value[i]=='\"')return i;}throw new FormatException("Unterminated Lua string.");}
 private static int SkipWhite(string value,int start){while(start<value.Length&&char.IsWhiteSpace(value[start]))start++;return start;}
 private static string Unescape(string value)=>LuaEscapePattern.Replace(value,match=>match.Groups[1].Value switch{"\\"=>"\\","\""=>"\"","n"=>"\n","r"=>"\r","t"=>"\t",_=>match.Value});
#pragma warning disable SYSLIB1045 // Runtime regex keeps generated implementation details out of per-layer coverage.
 private static readonly Regex ReturnDiffPattern=new(@"\breturn\s+diff\b",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
 private static readonly Regex ReturnModifiersPattern=new(@"\breturn\s+modifiers\b",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
 private static readonly Regex IndexedValuePattern=new("\\[\\d+\\]\\s*=\\s*\"((?:\\\\.|[^\"])*)\"",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
 private static readonly Regex IndexedNumberPattern=new(@"\[\d+\]\s*=\s*(-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
 private static readonly Regex LuaEscapePattern=new("\\\\([\\\\\"nrt])",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
#pragma warning restore SYSLIB1045
}
