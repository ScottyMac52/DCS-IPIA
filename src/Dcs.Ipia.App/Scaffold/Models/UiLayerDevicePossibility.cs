using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerDevicePossibility from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerDevicePossibility
{
 #region BEGIN Public
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = string.Empty;
    /// <summary>Gets or sets CanonicalDeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("canonicalDeviceId")] public string CanonicalDeviceId { get; set; } = string.Empty;
    /// <summary>Gets or sets Label for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    /// <summary>Gets or sets Modifier for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("modifier")] public string? Modifier { get; set; }
    /// <summary>Gets or sets OverlayStatus for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("overlayStatus")] public string OverlayStatus { get; set; } = string.Empty;
    /// <summary>Gets or sets Instances for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("instances")] public List<string> Instances { get; set; } = [];
    /// <summary>Gets or sets CatalogState for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("catalogState")] public string CatalogState { get; set; } = string.Empty;
    /// <summary>Gets or sets Applicability for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("applicability")] public string Applicability { get; set; } = string.Empty;
    /// <summary>Gets or sets InstanceDisplay for the DCS-Common UI Layer model.</summary>
    public string InstanceDisplay => Instances.Count == 0 ? "All" : string.Join(", ", Instances);

 #endregion END Public
}
