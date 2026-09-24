using System.Text.Json;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Returns a validated Huey module view while retaining every authored source byte.</summary>
public sealed class HueyImportResult {
 /// <summary>Initializes the read-only import result.</summary>
 /// <param name="configuration">The parsed aircraft profiles and modifiers.</param>
 /// <param name="instances">Physical device instances and roles.</param>
 /// <param name="pages">Summary and device kneeboard page identities.</param>
 /// <param name="usage">Referenced canonical UI Layer functions.</param>
 /// <param name="metadata">Parsed copies of the authored JSON files, keyed by repository path.</param>
 /// <param name="sources">The original source texts, including unprojected page callouts.</param>
 /// <param name="provenance">Exact repository, commit, path, and SHA-256 records.</param>
 /// <param name="diagnostics">Structured import diagnostics.</param>
 public HueyImportResult(ModuleConfiguration? configuration,IReadOnlyList<DeviceInstance> instances,IReadOnlyList<KneeboardPage> pages,IReadOnlyList<UiLayerModuleUsage> usage,IReadOnlyDictionary<string,JsonElement> metadata,IReadOnlyDictionary<string,string> sources,IReadOnlyList<SourceProvenance> provenance,IReadOnlyList<ImportDiagnostic> diagnostics) {
  Configuration=configuration;Instances=instances;Pages=pages;Usage=usage;Metadata=metadata;Sources=sources;Provenance=provenance;Diagnostics=diagnostics;
 }
 #region BEGIN Public
 /// <summary>Gets the parsed aircraft configuration.</summary>
 public ModuleConfiguration? Configuration {get;}
 /// <summary>Gets stable physical device instances.</summary>
 public IReadOnlyList<DeviceInstance> Instances {get;}
 /// <summary>Gets ordered kneeboard page identities.</summary>
 public IReadOnlyList<KneeboardPage> Pages {get;}
 /// <summary>Gets the canonical UI Layer functions used by the module.</summary>
 public IReadOnlyList<UiLayerModuleUsage> Usage {get;}
 /// <summary>Gets cloned JSON roots for all six module configuration files.</summary>
 public IReadOnlyDictionary<string,JsonElement> Metadata {get;}
 /// <summary>Gets the unchanged repository-relative source texts.</summary>
 public IReadOnlyDictionary<string,string> Sources {get;}
 /// <summary>Gets content hashes for every source.</summary>
 public IReadOnlyList<SourceProvenance> Provenance {get;}
 /// <summary>Gets validation failures or warnings.</summary>
 public IReadOnlyList<ImportDiagnostic> Diagnostics {get;}
 /// <summary>Gets whether the complete supplied input imported without an error.</summary>
 public bool Succeeded=>Configuration is not null && Diagnostics.All(x=>!x.IsError);
 #endregion END Public
}
