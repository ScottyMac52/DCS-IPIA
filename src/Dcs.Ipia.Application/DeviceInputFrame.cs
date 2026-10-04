namespace Dcs.Ipia.Application
{
    /// <summary>One complete physical input sample; Guid.Empty denotes foreground keyboard modifiers.</summary>
    /// <param name="PhysicalGuid">Native instance identity or keyboard.</param>
    /// <param name="Inputs">Complete supported input values; axes range from zero to one.</param>
    public sealed record DeviceInputFrame(Guid PhysicalGuid, IReadOnlyDictionary<string, double> Inputs);
}
