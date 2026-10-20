namespace Heimdall.App.Games
{
    /// <summary>An explicit preflight choice; a separate binding-impact review is still required before staging.</summary>
    /// <param name="DeviceId">Unresolved source reference.</param>
    /// <param name="Clear">Whether to clear its assignments and dependent chords.</param>
    /// <param name="PhysicalGuid">Selected replacement instance.</param>
    /// <param name="TargetReference">Explicit captured replacement index, when necessary.</param>
    internal sealed record GameExportResolution(string DeviceId, bool Clear, Guid PhysicalGuid = default, string? TargetReference = null);
}
