using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiHardwareChoice from the DCS-Common UI Layer editor.</summary>
public sealed class IpiHardwareChoice
{
 #region BEGIN Public
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>Gets or sets Label for the DCS-Common UI Layer model.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Gets or sets ToString for the DCS-Common UI Layer model.</summary>
    public override string ToString() => $"{Label} — {DeviceId}";

 #endregion END Public
}
