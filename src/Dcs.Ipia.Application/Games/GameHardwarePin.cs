namespace Dcs.Ipia.Application.Games
{
    /// <summary>An explicitly reviewed catalog choice and native unit; game product tokens never supply this evidence.</summary>
    /// <param name="HardwareId">Shared catalog definition, independent of the physical unit.</param>
    /// <param name="Device">Native observation retained for offline authoring.</param>
    public sealed record GameHardwarePin(string HardwareId, DetectedDcsDevice Device);
}
