using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents UiLayerBindingEdit from the DCS-Common UI Layer editor.</summary>
public sealed class UiLayerBindingEdit
{
 #region BEGIN Public
    /// <summary>Gets or sets Action for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("action")] public string Action { get; set; } = "upsert";
    /// <summary>Gets or sets Profile for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("profile")] public UiLayerProfileTarget? Profile { get; set; }
    /// <summary>Gets or sets Section for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("section")] public string Section { get; set; } = "keyDiffs";
    /// <summary>Gets or sets Key for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("key")] public string? Key { get; set; }
    /// <summary>Gets or sets Reformers for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("reformers")] public List<string> Reformers { get; set; } = [];
    /// <summary>Gets or sets Command for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("command")] public string Command { get; set; } = string.Empty;
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }
    /// <summary>Gets or sets Label for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("label")] public string? Label { get; set; }
    /// <summary>Gets or sets From for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("from")] public UiLayerPhysicalTarget? From { get; set; }
    /// <summary>Gets or sets To for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("to")] public UiLayerPhysicalTarget? To { get; set; }
    [JsonIgnore] public string Summary => Action == "delete-profile"
        ? $"delete profile: {Profile?.Category}/{Profile?.Filename}"
        : $"{Action}: {Command} → {Profile?.Filename ?? "functions.json"} {Key ?? To?.Key} {string.Join(" + ", Reformers.Count > 0 ? Reformers : To?.Reformers ?? [])}";

 #endregion END Public
}
