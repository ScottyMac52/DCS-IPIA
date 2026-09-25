using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerPhysicalTarget from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerPhysicalTarget
{
 #region BEGIN Public
    /// <summary>Gets or sets Key for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    /// <summary>Gets or sets Reformers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("reformers")] public List<string> Reformers { get; set; } = [];

 #endregion END Public
}
