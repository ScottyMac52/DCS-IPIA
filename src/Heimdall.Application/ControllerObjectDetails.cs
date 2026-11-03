namespace Heimdall.Application
{
    /// <summary>Raw DirectInput object metadata; native offsets are not custom state-buffer offsets or game indices.</summary>
    public sealed record ControllerObjectDetails(uint NativeOffset, uint Flags, uint? MaximumForce = null,
        uint? ForceResolution = null, ushort? CollectionNumber = null, ushort? DesignatorIndex = null,
        ushort? UsagePage = null, ushort? Usage = null, uint? Dimension = null, ushort? Exponent = null,
        ushort? ReportId = null)
    {
        #region BEGIN Public
        #endregion END Public
    }
}
