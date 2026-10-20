namespace Heimdall.Application
{
    /// <summary>One enumerated object mapped to the shared live-test state format.</summary>
    /// <param name="Evidence">Original native object evidence.</param>
    /// <param name="Kind">Supported input kind.</param>
    /// <param name="DcsKey">Stable DCS input identity; a hat has eight suffixed directional keys.</param>
    /// <param name="Offset">Offset in the 176-byte live-test state buffer.</param>
    public sealed record ControllerInput(ControllerObject Evidence, ControllerInputKind Kind, string DcsKey, int Offset);
}
