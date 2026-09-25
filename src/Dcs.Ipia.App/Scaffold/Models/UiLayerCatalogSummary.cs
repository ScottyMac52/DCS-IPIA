using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogSummary from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogSummary
{
 #region BEGIN Public
    /// <summary>Gets or sets Profiles for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("profiles")] public int Profiles { get; set; }
    /// <summary>Gets or sets Bindings for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("bindings")] public int Bindings { get; set; }
    /// <summary>Gets or sets Keys for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("keys")] public int Keys { get; set; }
    /// <summary>Gets or sets Axes for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("axes")] public int Axes { get; set; }
    /// <summary>Gets or sets Modifiers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("modifiers")] public int Modifiers { get; set; }
    /// <summary>Gets or sets Possibilities for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("possibilities")] public int Possibilities { get; set; }
    /// <summary>Gets or sets Errors for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("errors")] public int Errors { get; set; }

 #endregion END Public
}
