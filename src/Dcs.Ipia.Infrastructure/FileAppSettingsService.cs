using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Infrastructure;

/// <summary>Loads packaged defaults, overlays validated user preferences, and migrates theme.txt.</summary>
public sealed class FileAppSettingsService : IAppSettingsService {
 private const int SupportedSchemaVersion=1;
 private static readonly JsonSerializerOptions JsonOptions=new() {WriteIndented=true};
 private readonly string _userPath;
 private readonly string _legacyThemePath;

 /// <summary>Opens application defaults and the current user's writable settings location.</summary>
 /// <param name="defaultsPath">Packaged read-only appsettings.json.</param>
 /// <param name="userPath">Writable per-user JSON preferences path.</param>
 /// <param name="legacyThemePath">Previous theme.txt preference path.</param>
 public FileAppSettingsService(string defaultsPath,string userPath,string legacyThemePath) {
  ArgumentException.ThrowIfNullOrWhiteSpace(defaultsPath);
  ArgumentException.ThrowIfNullOrWhiteSpace(userPath);
  ArgumentException.ThrowIfNullOrWhiteSpace(legacyThemePath);
  _userPath=userPath;
  _legacyThemePath=legacyThemePath;
  var fallback=new AppSettings(SupportedSchemaVersion,AppearanceTheme.Light,OccupiedDestinationBehavior.Ask);
  var defaults=Read(defaultsPath,fallback,out _,out var defaultsError);
  var settings=Read(userPath,defaults,out var hasUserTheme,out var userError);
  Current=settings;
  LastError=JoinErrors(defaultsError,userError);
  if(!hasUserTheme && TryReadLegacyTheme(out var legacyTheme)) {
   Current=new AppSettings(SupportedSchemaVersion,legacyTheme,settings.OccupiedDestinationBehavior);
   TryPersist();
  }
 }

 #region BEGIN Public
 /// <inheritdoc />
 public AppSettings Current {get;private set;}
 /// <inheritdoc />
 public string? LastError {get;private set;}

 /// <inheritdoc />
 public bool TryUpdateTheme(AppearanceTheme theme) {
  if(!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
  Current=new AppSettings(SupportedSchemaVersion,theme,Current.OccupiedDestinationBehavior);
  return TryPersist();
 }

 /// <inheritdoc />
 public bool TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior behavior) {
  if(!Enum.IsDefined(behavior)) throw new ArgumentOutOfRangeException(nameof(behavior));
  Current=new AppSettings(SupportedSchemaVersion,Current.Theme,behavior);
  return TryPersist();
 }
 #endregion END Public

 #region BEGIN private helpers
 private static AppSettings Read(string path,AppSettings fallback,out bool validTheme,out string? error) {
  validTheme=false;
  error=null;
  if(!File.Exists(path)) return fallback;
  try {
   using var document=JsonDocument.Parse(File.ReadAllText(path));
   var root=document.RootElement;
   if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("SchemaVersion",out var version) ||
      version.ValueKind!=JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema!=SupportedSchemaVersion) {
    error=$"Unsupported settings schema in {Path.GetFileName(path)}; using defaults.";
    return fallback;
   }
   var theme=fallback.Theme;
   var policy=fallback.OccupiedDestinationBehavior;
   if(root.TryGetProperty("Theme",out var themeValue)) {
    if(TryParseEnum(themeValue,out AppearanceTheme parsed)) { theme=parsed;validTheme=true; }
    else error="Invalid Theme setting; using the validated preference or default.";
   }
   if(root.TryGetProperty("OccupiedDestinationBehavior",out var behaviorValue)) {
    if(TryParseEnum(behaviorValue,out OccupiedDestinationBehavior parsed)) policy=parsed;
    else error=JoinErrors(error,"Invalid OccupiedDestinationBehavior setting; using the default.");
   }
   return new AppSettings(SupportedSchemaVersion,theme,policy);
  }
  catch(JsonException) {error=$"Malformed settings JSON in {Path.GetFileName(path)}; using defaults.";}
  catch(IOException) {error=$"Unable to read {Path.GetFileName(path)}; using defaults.";}
  catch(UnauthorizedAccessException) {error=$"Unable to access {Path.GetFileName(path)}; using defaults.";}
  return fallback;
 }

 private static bool TryParseEnum<T>(JsonElement element,out T result) where T:struct,Enum {
  result=default;
  return element.ValueKind==JsonValueKind.String &&
    Enum.TryParse(element.GetString(),true,out result) && Enum.IsDefined(result);
 }

 private bool TryReadLegacyTheme(out AppearanceTheme theme) {
  theme=default;
  try {
   return File.Exists(_legacyThemePath) &&
    Enum.TryParse(File.ReadAllText(_legacyThemePath).Trim(),true,out theme) && Enum.IsDefined(theme);
  }
  catch(IOException) {LastError=JoinErrors(LastError,"Unable to migrate theme.txt.");}
  catch(UnauthorizedAccessException) {LastError=JoinErrors(LastError,"Unable to migrate theme.txt.");}
  return false;
 }

 private bool TryPersist() {
  var temp=_userPath+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
   var json=JsonSerializer.Serialize(new {
    SchemaVersion=SupportedSchemaVersion,
    Theme=Current.Theme.ToString(),
    OccupiedDestinationBehavior=Current.OccupiedDestinationBehavior.ToString()
   },JsonOptions);
   File.WriteAllText(temp,json);
   File.Move(temp,_userPath,true);
   LastError=null;
   return true;
  }
  catch(IOException) {LastError="Settings changed for this session but could not be saved.";}
  catch(UnauthorizedAccessException) {LastError="Settings changed for this session but could not be saved.";}
  finally {
   try {if(File.Exists(temp))File.Delete(temp);}
   catch(IOException) { /* The next launch does not consume temporary files. */ }
   catch(UnauthorizedAccessException) { /* The active preference stays in memory. */ }
  }
  return false;
 }

 private static string? JoinErrors(string? first,string? second) =>
  string.IsNullOrWhiteSpace(first)?second:string.IsNullOrWhiteSpace(second)?first:first+" "+second;
 #endregion END private helpers
}
