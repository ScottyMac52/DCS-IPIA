using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiUiLayerSelection from the DCS-Common UI Layer editor.</summary>
public sealed class IpiUiLayerSelection
{
 #region BEGIN Public
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceInstance for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceInstance")] public string? DeviceInstance { get; set; }
    /// <summary>Gets or sets FunctionId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("functionId")] public string FunctionId { get; set; } = string.Empty;
    /// <summary>Gets or sets Modifiers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("modifiers")] public List<string> Modifiers { get; set; } = [];
    [JsonIgnore] public string ModifierDisplay
    {
        get => string.Join(" + ", Modifiers);
        set => Modifiers = (value ?? string.Empty).Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

 #endregion END Public
}
