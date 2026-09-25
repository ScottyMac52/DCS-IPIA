using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerConsumerImpact from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerConsumerImpact
{
 #region BEGIN Public
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets Root for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("root")] public string Root { get; set; } = string.Empty;
    /// <summary>Gets or sets MatchedBindings for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("matchedBindings")] public List<string> MatchedBindings { get; set; } = [];

 #endregion END Public
}
