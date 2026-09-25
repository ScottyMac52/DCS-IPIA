using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiUiFunctionChoice from the DCS-Common UI Layer editor.</summary>
public sealed class IpiUiFunctionChoice
{
 #region BEGIN Public
    /// <summary>Gets or sets Id for the DCS-Common UI Layer model.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Gets or sets Command for the DCS-Common UI Layer model.</summary>
    public string Command { get; set; } = string.Empty;
    /// <summary>Gets or sets Label for the DCS-Common UI Layer model.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Gets or sets Category for the DCS-Common UI Layer model.</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>Gets or sets ToString for the DCS-Common UI Layer model.</summary>
    public override string ToString() => $"{Category} — {Label}";

 #endregion END Public
}
