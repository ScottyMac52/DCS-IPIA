using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiLayerChoice from the DCS-Common UI Layer editor.</summary>
public sealed class IpiLayerChoice
{
 #region BEGIN Public
    /// <summary>Gets or sets Name for the DCS-Common UI Layer model.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets DisplayName for the DCS-Common UI Layer model.</summary>
    public string DisplayName => string.IsNullOrEmpty(Name) ? "Base (no modifier)" : Name;
    /// <summary>Gets or sets ToString for the DCS-Common UI Layer model.</summary>
    public override string ToString() => DisplayName;

 #endregion END Public
}
