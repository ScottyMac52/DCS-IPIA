using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiUiLayerUtilization from the DCS-Common UI Layer editor.</summary>
public sealed class IpiUiLayerUtilization
{
 #region BEGIN Public
    /// <summary>Gets or sets Mode for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "explicit";
    /// <summary>Gets or sets Bindings for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("bindings")] public List<IpiUiLayerSelection> Bindings { get; set; } = [];

 #endregion END Public
}
