using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiModuleAuthoringState from the DCS-Common UI Layer editor.</summary>
public sealed class IpiModuleAuthoringState
{
 #region BEGIN Public
    /// <summary>Gets or sets Fingerprint for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets Initialized for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("initialized")] public bool Initialized { get; set; }
    /// <summary>Gets or sets Modifiers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("modifiers")] public List<IpiModifierDefinition> Modifiers { get; set; } = [];
    /// <summary>Gets or sets UiLayerUtilization for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("uiLayerUtilization")] public IpiUiLayerUtilization? UiLayerUtilization { get; set; }

 #endregion END Public
}
