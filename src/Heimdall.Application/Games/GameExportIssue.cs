namespace Heimdall.Application.Games
{
    /// <summary>An unresolved native reference which must be reviewed before writing a game preset.</summary>
    /// <param name="DeviceId">Logical device to select for repair.</param>
    /// <param name="Reference">Native token and captured index.</param>
    /// <param name="Reason">Why the reference cannot be resolved.</param>
    /// <param name="Assignments">Every native field affected by this reference.</param>
    /// <param name="CanRepair">Whether the captured portable device can be safely removed or replaced.</param>
    public sealed record GameExportIssue(string DeviceId, string Reference, string Reason, IReadOnlyList<string> Assignments, bool CanRepair);
}
