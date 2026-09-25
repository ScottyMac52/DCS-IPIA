using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerCatalogBinding from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerCatalogBinding
{
 #region BEGIN Public
    /// <summary>Gets or sets Profile for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("profile")] public string Profile { get; set; } = string.Empty;
    /// <summary>Gets or sets Category for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    /// <summary>Gets or sets Section for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("section")] public string Section { get; set; } = string.Empty;
    /// <summary>Gets or sets Command for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("command")] public string Command { get; set; } = string.Empty;
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets Key for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    /// <summary>Gets or sets Chord for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("chord")] public string Chord { get; set; } = string.Empty;
    /// <summary>Gets or sets CatalogState for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("catalogState")] public string CatalogState { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }
    /// <summary>Gets or sets DeviceInstance for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceInstance")] public string? DeviceInstance { get; set; }
    /// <summary>Gets or sets FunctionId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("functionId")] public string? FunctionId { get; set; }
    /// <summary>Gets or sets ControlId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("controlId")] public string? ControlId { get; set; }
    /// <summary>Gets or sets HardwareLabel for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("hardwareLabel")] public string? HardwareLabel { get; set; }
    /// <summary>Gets or sets BindingId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("bindingId")] public string BindingId { get; set; } = string.Empty;
    /// <summary>Gets or sets Modifiers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("modifiers")] public List<string> Modifiers { get; set; } = [];

 #endregion END Public
}
