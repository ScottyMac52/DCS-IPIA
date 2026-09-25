using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiAuthoredDeviceDefinition from the DCS-Common UI Layer editor.</summary>
public sealed class IpiAuthoredDeviceDefinition
{
 #region BEGIN Public
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = string.Empty;
    /// <summary>Gets or sets ProfileFile for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("profileFile")] public string ProfileFile { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceInstance for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("deviceInstance")] public string? DeviceInstance { get; set; }
    /// <summary>Gets or sets Role for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("role")] public string? Role { get; set; }

 #endregion END Public
}
