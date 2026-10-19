namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// A modifier that must be active for a binding to fire.
/// </summary>
public sealed class Modifier
{
    /// <summary>Gets or sets the source of the modifier (button or axis name).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional value or threshold associated with the modifier.</summary>
    public string? Value { get; set; }
}
