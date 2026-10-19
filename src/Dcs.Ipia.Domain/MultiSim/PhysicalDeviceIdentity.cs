namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// Identity information for a physical input device, with room for sim-specific identifiers.
/// </summary>
public sealed class PhysicalDeviceIdentity
{
    /// <summary>Gets or sets the Windows device name.</summary>
    public string? WindowsName { get; set; }

    /// <summary>Gets or sets the USB vendor ID.</summary>
    public ushort VendorId { get; set; }

    /// <summary>Gets or sets the USB product ID.</summary>
    public ushort ProductId { get; set; }

    /// <summary>Gets or sets the Windows/DirectInput GUID.</summary>
    public string? Guid { get; set; }

    /// <summary>Gets or sets the composite ID (used by MSFS and similar).</summary>
    public int CompositeId { get; set; }

    /// <summary>Gets or sets sim-specific identifiers (e.g. DCS UUID, MSFS ProductID string, SC js slot).</summary>
    public Dictionary<string, string> SimSpecificIds { get; set; } = new();
}
