using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogComparison from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogComparison
{
 #region BEGIN Public
    /// <summary>Gets or sets Canonical for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("canonical")] public UiLayerCatalogDocument Canonical { get; set; } = new();
    /// <summary>Gets or sets Source for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("source")] public UiLayerCatalogDocument Source { get; set; } = new();
    /// <summary>Gets or sets Changes for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changes")] public List<UiLayerCatalogChange> Changes { get; set; } = [];

 #endregion END Public
}
