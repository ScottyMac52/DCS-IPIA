namespace Dcs.Ipia.Application
{
    /// <summary>Replaceable current-machine discovery evidence; never changes module membership.</summary>
    /// <param name="Device">Exact physical instance and native name.</param>
    /// <param name="Capabilities">Verified enumeration, or null on per-device failure.</param>
    /// <param name="Error">Actionable per-device failure explanation.</param>
    public sealed record ControllerObservation(DetectedDcsDevice Device, ControllerCapabilities? Capabilities, string? Error = null);
}
