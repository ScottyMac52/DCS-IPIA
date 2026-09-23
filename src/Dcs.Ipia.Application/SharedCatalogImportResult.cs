using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Contains normalized shared catalogs, provenance, and structured import diagnostics.</summary>
public sealed class SharedCatalogImportResult {
 /// <summary>Initializes the import result.</summary>
 /// <param name="devices">The imported devices.</param>
 /// <param name="catalog">The imported command catalog, if valid.</param>
 /// <param name="commands">The imported commands.</param>
 /// <param name="templates">The imported overlay templates.</param>
 /// <param name="provenance">The source provenance.</param>
 /// <param name="diagnostics">The import diagnostics.</param>
 public SharedCatalogImportResult(IReadOnlyList<PhysicalDevice> devices,CommandCatalog? catalog,IReadOnlyList<DcsCommandDefinition> commands,IReadOnlyList<UiLayerTemplate> templates,IReadOnlyList<SourceProvenance> provenance,IReadOnlyList<ImportDiagnostic> diagnostics) {
  Devices=devices;Catalog=catalog;Commands=commands;Templates=templates;Provenance=provenance;Diagnostics=diagnostics;
 }
 #region BEGIN Public
 /// <summary>Gets the imported physical devices.</summary>
 public IReadOnlyList<PhysicalDevice> Devices {get;}
 /// <summary>Gets the UI Layer command catalog.</summary>
 public CommandCatalog? Catalog {get;}
 /// <summary>Gets the UI Layer commands.</summary>
 public IReadOnlyList<DcsCommandDefinition> Commands {get;}
 /// <summary>Gets the canonical UI Layer overlay templates.</summary>
 public IReadOnlyList<UiLayerTemplate> Templates {get;}
 /// <summary>Gets the source provenance, including content hashes.</summary>
 public IReadOnlyList<SourceProvenance> Provenance {get;}
 /// <summary>Gets structured diagnostics.</summary>
 public IReadOnlyList<ImportDiagnostic> Diagnostics {get;}
 /// <summary>Gets whether every source was imported without errors.</summary>
 public bool Succeeded => Catalog is not null && !Diagnostics.Any(x=>x.IsError);
 #endregion END Public
}
