namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// Binding of a button, hat, or other digital input to a catalog command.
/// </summary>
public sealed class ButtonBinding
{
    /// <summary>Gets or sets the source button identifier (e.g. "Button1", "POV1_Up").</summary>
    public string SourceButton { get; set; } = string.Empty;

    /// <summary>Gets or sets the catalog command identifier this input is bound to.</summary>
    public string CommandId { get; set; } = string.Empty;

    /// <summary>Gets or sets any modifiers required for this binding.</summary>
    public List<Modifier> Modifiers { get; set; } = new();

    /// <summary>Gets or sets the binding mode (press, hold, toggle, etc.).</summary>
    public BindingMode Mode { get; set; }

    /// <summary>Gets or sets optional sim-specific button parameters.</summary>
    public Dictionary<string, object> SimSpecific { get; set; } = new();
}
