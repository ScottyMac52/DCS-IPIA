namespace Dcs.Ipia.Application
{
    /// <summary>Explains why a native input cannot safely map to a supported DCS identity.</summary>
    /// <param name="Evidence">Original enumerated object.</param>
    /// <param name="Reason">Unsupported or ambiguous mapping explanation.</param>
    public sealed record UnsupportedControllerObject(ControllerObject Evidence, string Reason);
}
