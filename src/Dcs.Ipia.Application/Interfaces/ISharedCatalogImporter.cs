namespace Dcs.Ipia.Application.Interfaces;

/// <summary>Imports canonical DCS-Common shared authored data from supplied source text.</summary>
public interface ISharedCatalogImporter {
 #region BEGIN Public
 /// <summary>Imports shared hardware, UI Layer commands, and overlay templates without modifying sources.</summary>
 /// <param name="request">The pinned source texts and paths.</param>
 /// <returns>Normalized data and diagnostics.</returns>
 SharedCatalogImportResult Import(SharedCatalogImportRequest request);
 #endregion END Public
}
