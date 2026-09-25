using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogDocument from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogDocument
{
 #region BEGIN Public
    /// <summary>Gets or sets Root for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("root")] public string Root { get; set; } = string.Empty;
    /// <summary>Gets or sets Scope for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
    /// <summary>Gets or sets Fingerprint for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets Valid for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("valid")] public bool Valid { get; set; }
    /// <summary>Gets or sets Profiles for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("profiles")] public List<UiLayerCatalogProfile> Profiles { get; set; } = [];
    /// <summary>Gets or sets Bindings for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("bindings")] public List<UiLayerCatalogBinding> Bindings { get; set; } = [];
    /// <summary>Gets or sets Modifiers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("modifiers")] public List<UiLayerCatalogModifier> Modifiers { get; set; } = [];
    /// <summary>Gets or sets Possibilities for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("possibilities")] public List<UiLayerDevicePossibility> Possibilities { get; set; } = [];
    /// <summary>Gets or sets Errors for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("errors")] public List<string> Errors { get; set; } = [];
    /// <summary>Gets or sets Warnings for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
    /// <summary>Gets or sets ChangedFiles for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changedFiles")] public List<string> ChangedFiles { get; set; } = [];
    /// <summary>Gets or sets Impact for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("impact")] public UiLayerImpactReport? Impact { get; set; }
    /// <summary>Gets or sets Summary for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("summary")] public UiLayerCatalogSummary Summary { get; set; } = new();

 #endregion END Public
}
