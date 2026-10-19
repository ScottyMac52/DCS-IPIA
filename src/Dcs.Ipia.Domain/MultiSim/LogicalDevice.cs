namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// A logical input device (stick, throttle, pedals, etc.) independent of any specific sim.
/// </summary>
public sealed class LogicalDevice
{
    /// <summary>Gets or sets a stable logical identifier (e.g. "Stick", "Throttle").</summary>
    public string LogicalId { get; set; } = string.Empty;

    /// <summary>Gets or sets the physical device identity resolved for this logical device.</summary>
    public PhysicalDeviceIdentity? Identity { get; set; }

    /// <summary>Gets or sets axis bindings for this device.</summary>
    public List<AxisBinding> Axes { get; set; } = new();

    /// <summary>Gets or sets button/hat bindings for this device.</summary>
    public List<ButtonBinding> Buttons { get; set; } = new();
}
