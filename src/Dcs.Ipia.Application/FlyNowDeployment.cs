namespace Dcs.Ipia.Application
{
    /// <summary>The active module and exact deployed footprint, retained independently of authoring data.</summary>
    /// <param name="ModuleId">Stable database module identity.</param>
    /// <param name="DisplayName">Module name captured at deployment.</param>
    /// <param name="Destination">Original absolute DCS Saved Games instance folder.</param>
    /// <param name="Files">DCS-relative paths owned by this activation.</param>
    /// <param name="Clearing">Whether cleanup must finish before repeat deployment.</param>
    public sealed record FlyNowDeployment(Guid ModuleId, string DisplayName, string Destination,
        IReadOnlyList<string> Files, bool Clearing = false);
}
