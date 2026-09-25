using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerProfileTarget from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerProfileTarget
{
 #region BEGIN Public
    /// <summary>Gets or sets Category for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("category")] public string Category { get; set; } = "joystick";
    /// <summary>Gets or sets Filename for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("filename")] public string Filename { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = string.Empty;

 #endregion END Public
}
