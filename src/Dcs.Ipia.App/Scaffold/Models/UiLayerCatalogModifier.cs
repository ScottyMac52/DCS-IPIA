using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogModifier from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogModifier
{
 #region BEGIN Public
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets Device for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("device")] public string Device { get; set; } = string.Empty;
    /// <summary>Gets or sets Key for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    /// <summary>Gets or sets Mode for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = string.Empty;
    /// <summary>Gets or sets CatalogState for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("catalogState")] public string CatalogState { get; set; } = string.Empty;

 #endregion END Public
}
