namespace Heimdall.WarThunder
{
    /// <summary>A uniquely resolved native mapping/local index, independent of physical controller order.</summary>
    public sealed record WarThunderNativeInputTarget(string DeviceId, int Index);
}
