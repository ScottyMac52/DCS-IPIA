namespace Dcs.Ipia.Domain
{
    /// <summary>Retains a canonical hardware control and its exact DCS input spelling.</summary>
    /// <param name="Control">The stable control identity and kind.</param>
    /// <param name="InputKey">The authored DCS input key.</param>
    /// <param name="SourceType">The exact authored hardware control type.</param>
    /// <param name="Group">The optional physical group.</param>
    /// <param name="Position">The optional position within the group.</param>
    public sealed record SharedHardwareControl(DeviceControl Control, string InputKey, string SourceType, string? Group,
                                               string? Position);
}
