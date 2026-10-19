namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// Binding of a physical axis to a catalog command.
/// </summary>
public sealed class AxisBinding
{
    /// <summary>Gets or sets the source axis name (e.g. "X", "Y", "rZ", "Slider0").</summary>
    public string SourceAxis { get; set; } = string.Empty;

    /// <summary>Gets or sets the catalog command identifier this axis is bound to.</summary>
    public string CommandId { get; set; } = string.Empty;

    /// <summary>Gets or sets the sensitivity (1.0 = default).</summary>
    public float Sensitivity { get; set; } = 1f;

    /// <summary>Gets or sets the deadzone (0–1 range, sim-specific interpretation).</summary>
    public float Deadzone { get; set; }

    /// <summary>Gets or sets a value indicating whether the axis is inverted.</summary>
    public bool Invert { get; set; }

    /// <summary>Gets or sets the response curve type.</summary>
    public CurveType Curve { get; set; }

    /// <summary>Gets or sets optional sim-specific axis parameters.</summary>
    public Dictionary<string, object> SimSpecific { get; set; } = new();
}
