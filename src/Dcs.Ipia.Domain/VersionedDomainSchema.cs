namespace Dcs.Ipia.Domain;

public readonly record struct DomainSchemaVersion(int Major,int Minor) {
 public override string ToString()=>$"{Major}.{Minor}";
}

public enum ControlKind { Button,Axis,Hat,Rocker,Switch,Encoder,Keyboard }
public enum KneeboardPageKind { Device,Summary }
public enum AssetKind { Source,Generated,ReleaseOnly }

public sealed record GraphicGeometry(double X,double Y,double Width,double Height,string CoordinateSpace);
public sealed record PhysicalDevice(Guid Id,string StableKey,string DisplayName,IReadOnlyList<string> Aliases);
public sealed record DeviceControl(Guid Id,Guid DeviceId,string StableKey,string Label,ControlKind Kind,GraphicGeometry? Geometry);
public sealed record DeviceInstance(Guid Id,Guid ModuleId,Guid DeviceId,string StableKey,string Role);
public sealed record CommandCatalog(Guid Id,string StableKey,string Source,string Version);
public sealed record DcsCommandDefinition(Guid Id,Guid CatalogId,string DcsCommandId,string Name,string Category,bool IsAxis);
public sealed record BindingRelationship(Guid Id,Guid ProfileId,Guid ControlId,Guid CommandId,BindingDisposition Disposition,string InputKey,IReadOnlyList<string> ModifierChord,AxisFilter? AxisTuning);
public sealed record UiLayerModuleUsage(Guid ModuleId,Guid CommandId,bool Enabled);
public sealed record UiLayerOverlay(Guid ModuleId,Guid DeviceId,Guid ControlId,Guid? CommandId,string Label);
public sealed record UiLayerTombstone(Guid ModuleId,string StableKey,string Reason);
public sealed record KneeboardCategory(Guid Id,string StableKey,string Label,int Order);
public sealed record KneeboardPage(Guid Id,Guid ModuleId,Guid? DeviceInstanceId,KneeboardPageKind Kind,string StableKey,string Title,IReadOnlyList<Guid> CategoryIds,int Order);
public sealed record DocumentationMetadata(Guid ModuleId,string OutputPath,string Title);
public sealed record PackagingMetadata(Guid ModuleId,string PackageId,string CompleteRoot,string OvGmeRoot,IReadOnlyList<string> IncludePatterns);
public sealed record SourceProvenance(string Repository,string Commit,string Path,string Sha256);
public sealed record SnapshotIdentity(int SchemaVersion,string Sha256);
public sealed record ReleaseVersion(int Major,int Minor,int Patch,int Revision=0) {
 public override string ToString()=>Revision==0?$"v{Major}.{Minor}.{Patch}":$"v{Major}.{Minor}.{Patch}.{Revision}";
}

public sealed class VersionedDomainSchema {
 public static readonly DomainSchemaVersion CurrentVersion=new(1,0);
 public const int CurrentMigrationVersion=2;
 public const int CurrentSnapshotVersion=1;

 public VersionedDomainSchema(
  IEnumerable<DcsModule> modules,
  IEnumerable<PhysicalDevice> devices,
  IEnumerable<DeviceControl> controls,
  IEnumerable<DeviceInstance> instances,
  IEnumerable<DeviceProfile> profiles,
  IEnumerable<CommandCatalog> catalogs,
  IEnumerable<DcsCommandDefinition> commands,
  IEnumerable<BindingRelationship> bindings,
  IEnumerable<UiLayerModuleUsage> uiLayerUsage,
  IEnumerable<UiLayerOverlay> uiLayerOverlays,
  IEnumerable<UiLayerTombstone> uiLayerTombstones,
  IEnumerable<KneeboardCategory> categories,
  IEnumerable<KneeboardPage> pages,
  IEnumerable<DocumentationMetadata> documentation,
  IEnumerable<PackagingMetadata> packages,
  IEnumerable<SourceProvenance> provenance,
  SnapshotIdentity snapshot,
  ReleaseVersion release) {
  Modules=Required(modules,nameof(modules)).OrderBy(x=>x.DcsId,StringComparer.Ordinal).ToArray();
  Devices=Required(devices,nameof(devices)).Select(x=>x with{Aliases=x.Aliases.ToArray()}).OrderBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Controls=Required(controls,nameof(controls)).OrderBy(x=>x.DeviceId).ThenBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Instances=Required(instances,nameof(instances)).OrderBy(x=>x.ModuleId).ThenBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Profiles=Required(profiles,nameof(profiles)).OrderBy(x=>x.DeviceId,StringComparer.Ordinal).ThenBy(x=>x.Instance,StringComparer.Ordinal).ToArray();
  Catalogs=Required(catalogs,nameof(catalogs)).OrderBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Commands=Required(commands,nameof(commands)).OrderBy(x=>x.CatalogId).ThenBy(x=>x.DcsCommandId,StringComparer.Ordinal).ToArray();
  Bindings=Required(bindings,nameof(bindings)).Select(x=>x with{ModifierChord=x.ModifierChord.ToArray()}).OrderBy(x=>x.ProfileId).ThenBy(x=>x.CommandId).ThenBy(x=>x.ControlId).ToArray();
  UiLayerUsage=Required(uiLayerUsage,nameof(uiLayerUsage)).OrderBy(x=>x.ModuleId).ThenBy(x=>x.CommandId).ToArray();
  UiLayerOverlays=Required(uiLayerOverlays,nameof(uiLayerOverlays)).OrderBy(x=>x.ModuleId).ThenBy(x=>x.DeviceId).ThenBy(x=>x.ControlId).ToArray();
  UiLayerTombstones=Required(uiLayerTombstones,nameof(uiLayerTombstones)).OrderBy(x=>x.ModuleId).ThenBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Categories=Required(categories,nameof(categories)).OrderBy(x=>x.Order).ThenBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Pages=Required(pages,nameof(pages)).Select(x=>x with{CategoryIds=x.CategoryIds.ToArray()}).OrderBy(x=>x.ModuleId).ThenBy(x=>x.Order).ThenBy(x=>x.StableKey,StringComparer.Ordinal).ToArray();
  Documentation=Required(documentation,nameof(documentation)).OrderBy(x=>x.ModuleId).ToArray();
  Packages=Required(packages,nameof(packages)).Select(x=>x with{IncludePatterns=x.IncludePatterns.ToArray()}).OrderBy(x=>x.ModuleId).ToArray();
  Provenance=Required(provenance,nameof(provenance)).OrderBy(x=>x.Repository,StringComparer.Ordinal).ThenBy(x=>x.Path,StringComparer.Ordinal).ToArray();
  Snapshot=snapshot??throw new ArgumentNullException(nameof(snapshot)); Release=release??throw new ArgumentNullException(nameof(release));
  Validate();
 }
 public DomainSchemaVersion SchemaVersion=>CurrentVersion;
 public int MigrationVersion=>CurrentMigrationVersion;
 public IReadOnlyList<DcsModule> Modules{get;} public IReadOnlyList<PhysicalDevice> Devices{get;} public IReadOnlyList<DeviceControl> Controls{get;}
 public IReadOnlyList<DeviceInstance> Instances{get;} public IReadOnlyList<DeviceProfile> Profiles{get;} public IReadOnlyList<CommandCatalog> Catalogs{get;}
 public IReadOnlyList<DcsCommandDefinition> Commands{get;} public IReadOnlyList<BindingRelationship> Bindings{get;}
 public IReadOnlyList<UiLayerModuleUsage> UiLayerUsage{get;} public IReadOnlyList<UiLayerOverlay> UiLayerOverlays{get;} public IReadOnlyList<UiLayerTombstone> UiLayerTombstones{get;}
 public IReadOnlyList<KneeboardCategory> Categories{get;} public IReadOnlyList<KneeboardPage> Pages{get;}
 public IReadOnlyList<DocumentationMetadata> Documentation{get;} public IReadOnlyList<PackagingMetadata> Packages{get;}
 public IReadOnlyList<SourceProvenance> Provenance{get;} public SnapshotIdentity Snapshot{get;} public ReleaseVersion Release{get;}

 private void Validate() {
  Unique(Modules.Select(x=>x.Id),"module ID"); Unique(Modules.Select(x=>x.DcsId),"DCS input module ID",StringComparer.OrdinalIgnoreCase);
  Unique(Devices.Select(x=>x.Id),"device ID"); Unique(Devices.Select(x=>x.StableKey),"device stable key",StringComparer.OrdinalIgnoreCase);
  Unique(Controls.Select(x=>x.Id),"control ID"); Unique(Controls.Select(x=>$"{x.DeviceId}\0{x.StableKey}"),"control stable key",StringComparer.OrdinalIgnoreCase);
  Unique(Instances.Select(x=>x.Id),"device instance ID"); Unique(Instances.Select(x=>$"{x.ModuleId}\0{x.StableKey}"),"module device instance",StringComparer.OrdinalIgnoreCase);
  Unique(Profiles.Select(x=>x.Id),"profile ID"); Unique(Catalogs.Select(x=>x.Id),"catalog ID"); Unique(Commands.Select(x=>x.Id),"command ID"); Unique(Bindings.Select(x=>x.Id),"binding ID");
  Unique(Categories.Select(x=>x.Id),"kneeboard category ID"); Unique(Pages.Select(x=>x.Id),"kneeboard page ID");
  var moduleIds=Modules.Select(x=>x.Id).ToHashSet(); var deviceIds=Devices.Select(x=>x.Id).ToHashSet(); var controlIds=Controls.Select(x=>x.Id).ToHashSet();
  var profileIds=Profiles.Select(x=>x.Id).ToHashSet(); var catalogIds=Catalogs.Select(x=>x.Id).ToHashSet(); var commandIds=Commands.Select(x=>x.Id).ToHashSet();
  var instanceIds=Instances.Select(x=>x.Id).ToHashSet(); var categoryIds=Categories.Select(x=>x.Id).ToHashSet();
  Require(Controls,x=>deviceIds.Contains(x.DeviceId),"control device");
  Require(Instances,x=>moduleIds.Contains(x.ModuleId)&&deviceIds.Contains(x.DeviceId),"device instance");
  Require(Commands,x=>catalogIds.Contains(x.CatalogId),"command catalog");
  Require(Bindings,x=>profileIds.Contains(x.ProfileId)&&controlIds.Contains(x.ControlId)&&commandIds.Contains(x.CommandId),"binding");
  Require(Bindings.Where(x=>x.AxisTuning is not null),x=>Commands.Single(c=>c.Id==x.CommandId).IsAxis,"axis tuning command");
  Require(UiLayerUsage,x=>moduleIds.Contains(x.ModuleId)&&commandIds.Contains(x.CommandId),"UI Layer utilization");
  Require(UiLayerOverlays,x=>moduleIds.Contains(x.ModuleId)&&deviceIds.Contains(x.DeviceId)&&controlIds.Contains(x.ControlId)&&(x.CommandId is null||commandIds.Contains(x.CommandId.Value)),"UI Layer overlay");
  Require(UiLayerTombstones,x=>moduleIds.Contains(x.ModuleId),"UI Layer tombstone");
  Require(Pages,x=>moduleIds.Contains(x.ModuleId)&&(x.DeviceInstanceId is null||instanceIds.Contains(x.DeviceInstanceId.Value))&&x.CategoryIds.All(categoryIds.Contains),"kneeboard page");
  Require(Documentation,x=>moduleIds.Contains(x.ModuleId),"documentation metadata"); Require(Packages,x=>moduleIds.Contains(x.ModuleId),"packaging metadata");
  if(Snapshot.SchemaVersion!=CurrentSnapshotVersion) throw new ArgumentException("Unsupported snapshot schema version.",nameof(Snapshot));
 }
 private static IReadOnlyList<T> Required<T>(IEnumerable<T>? values,string name)=>values?.ToArray()??throw new ArgumentNullException(name);
 private static void Unique<T>(IEnumerable<T> values,string label,IEqualityComparer<T>? comparer=null){var all=values.ToArray();if(all.Distinct(comparer).Count()!=all.Length)throw new ArgumentException($"Duplicate {label}.");}
 private static void Require<T>(IEnumerable<T> values,Func<T,bool> predicate,string label){if(values.Any(x=>!predicate(x)))throw new ArgumentException($"Invalid {label} relationship.");}
}
