using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiModifierDefinition from the DCS-Common UI Layer editor.</summary>
public sealed class IpiModifierDefinition
{
 #region BEGIN Public
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets Device for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("device")] public string Device { get; set; } = string.Empty;
    /// <summary>Gets or sets Key for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    /// <summary>Gets or sets Mode for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "hold";
    /// <summary>Gets or sets SemanticModifier for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("semanticModifier")] public string? SemanticModifier { get; set; }
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }

 #endregion END Public
}
