using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogChange from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogChange
{
 #region BEGIN Public
    /// <summary>Gets or sets RelativePath for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = string.Empty;
    /// <summary>Gets or sets State for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    /// <summary>Gets or sets Action for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("action")] public string Action { get; set; } = "Keep";

 #endregion END Public
}
