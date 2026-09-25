using System.Text.Json.Serialization;

namespace DcsConsumerScaffold.Models;

/// <summary>Represents IpiUiLayerBindingChoice from the DCS-Common UI Layer editor.</summary>
public sealed class IpiUiLayerBindingChoice
{
 #region BEGIN Public
    /// <summary>Gets or sets IsSelected for the DCS-Common UI Layer model.</summary>
    public bool IsSelected { get; set; }
    /// <summary>Gets or sets BindingId for the DCS-Common UI Layer model.</summary>
    public string BindingId { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceId for the DCS-Common UI Layer model.</summary>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>Gets or sets DeviceInstance for the DCS-Common UI Layer model.</summary>
    public string? DeviceInstance { get; set; }
    /// <summary>Gets or sets FunctionId for the DCS-Common UI Layer model.</summary>
    public string FunctionId { get; set; } = string.Empty;
    /// <summary>Gets or sets FunctionLabel for the DCS-Common UI Layer model.</summary>
    public string FunctionLabel { get; set; } = string.Empty;
    /// <summary>Gets or sets ControlId for the DCS-Common UI Layer model.</summary>
    public string ControlId { get; set; } = string.Empty;
    /// <summary>Gets or sets HardwareLabel for the DCS-Common UI Layer model.</summary>
    public string HardwareLabel { get; set; } = string.Empty;
    /// <summary>Gets or sets Modifiers for the DCS-Common UI Layer model.</summary>
    public List<string> Modifiers { get; set; } = [];
    /// <summary>Gets or sets ModifierDisplay for the DCS-Common UI Layer model.</summary>
    public string ModifierDisplay => Modifiers.Count == 0 ? "Base" : string.Join(" + ", Modifiers);
    /// <summary>Gets or sets ToSelection for the DCS-Common UI Layer model.</summary>
    public IpiUiLayerSelection ToSelection() => new()
    {
        DeviceId = DeviceId, DeviceInstance = DeviceInstance, FunctionId = FunctionId, Modifiers = [.. Modifiers],
    };

 #endregion END Public
}
