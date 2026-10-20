namespace Heimdall.Domain
{
    /// <summary>Preserves an instance recognition rule for a shared device.</summary>
    /// <param name="DeviceKey">The shared device key.</param>
    /// <param name="Pattern">The source regular expression.</param>
    /// <param name="Group">The capture group holding the instance number.</param>
    public sealed record DeviceInstancePattern(string DeviceKey, string Pattern, int Group);
}
