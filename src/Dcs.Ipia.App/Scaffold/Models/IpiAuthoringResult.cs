using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiAuthoringResult from the DCS-Common UI Layer editor.</summary>
public sealed class IpiAuthoringResult
{
 #region BEGIN Public
    /// <summary>Gets or sets Fingerprint for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("fingerprint")] public string? Fingerprint { get; set; }
    /// <summary>Gets or sets ProfilesDirectory for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("profilesDirectory")] public string? ProfilesDirectory { get; set; }
    /// <summary>Gets or sets ChangedFiles for the DCS-Common UI Layer model.</summary>
    [JsonPropertyName("changedFiles")] public List<string> ChangedFiles { get; set; } = [];

 #endregion END Public
}
