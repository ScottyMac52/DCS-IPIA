using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerImpactReport from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerImpactReport
{
 #region BEGIN Public
    /// <summary>Gets or sets ChangedDevices for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changedDevices")] public List<string> ChangedDevices { get; set; } = [];
    /// <summary>Gets or sets ChangedFunctions for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changedFunctions")] public List<string> ChangedFunctions { get; set; } = [];
    /// <summary>Gets or sets ChangedBindings for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changedBindings")] public List<string> ChangedBindings { get; set; } = [];
    /// <summary>Gets or sets ChangedModifierFamilies for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changedModifierFamilies")] public List<string> ChangedModifierFamilies { get; set; } = [];
    /// <summary>Gets or sets ExplicitConsumers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("explicitConsumers")] public List<UiLayerConsumerImpact> ExplicitConsumers { get; set; } = [];
    /// <summary>Gets or sets CompatibilityConsumers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("compatibilityConsumers")] public List<UiLayerConsumerImpact> CompatibilityConsumers { get; set; } = [];
    /// <summary>Gets or sets Summary for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;

 #endregion END Public
}
