using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiSharedControlChoice from the DCS-Common UI Layer editor.</summary>
public sealed class IpiSharedControlChoice
{
 #region BEGIN Public
    /// <summary>Gets or sets Id for the DCS-Common UI Layer model.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Gets or sets Key for the DCS-Common UI Layer model.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Gets or sets Type for the DCS-Common UI Layer model.</summary>
    public string Type { get; set; } = "button";
    /// <summary>Gets or sets HardwareLabel for the DCS-Common UI Layer model.</summary>
    public string HardwareLabel { get; set; } = string.Empty;
    /// <summary>Gets or sets Section for the DCS-Common UI Layer model.</summary>
    public string Section => Type == "axis" ? "axisDiffs" : "keyDiffs";
    /// <summary>Gets or sets ToString for the DCS-Common UI Layer model.</summary>
    public override string ToString() => $"{HardwareLabel} — {Id}";

 #endregion END Public
}
