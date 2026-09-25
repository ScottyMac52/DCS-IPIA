using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogProfile from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogProfile
{
 #region BEGIN Public
    /// <summary>Gets or sets Category for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets RelativePath for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = string.Empty;
    /// <summary>Gets or sets Fingerprint for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets KeyCount for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("keyCount")] public int KeyCount { get; set; }
    /// <summary>Gets or sets AxisCount for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("axisCount")] public int AxisCount { get; set; }
    /// <summary>Gets or sets EffectiveCount for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("effectiveCount")] public int EffectiveCount { get; set; }
    /// <summary>Gets or sets CatalogState for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("catalogState")] public string CatalogState { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }
    /// <summary>Gets or sets DeviceInstance for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceInstance")] public string? DeviceInstance { get; set; }
    /// <summary>Gets or sets ToString for the DCS-Common UI Layer model.</summary>
    public override string ToString() => $"{Category} — {Name}";

 #endregion END Public
}
