namespace Heimdall.Application
{
    /// <summary>Validated deployable DCS files generated from one saved module.</summary>
    /// <param name="DisplayName">Saved module display name.</param>
    /// <param name="Files">Exact DCS-relative paths and bytes.</param>
    public sealed record FlyNowPayload(string DisplayName, IReadOnlyDictionary<string, byte[]> Files);
}
