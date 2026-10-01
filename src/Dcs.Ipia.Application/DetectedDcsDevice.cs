namespace Dcs.Ipia.Application
{
    /// <summary>A Windows controller instance or a previously captured DCS profile.</summary>
    /// <param name="PhysicalGuid">DirectInput instance GUID, independent of workspace IDs.</param>
    /// <param name="Name">Native controller name.</param>
    /// <param name="Connected">Whether DirectInput currently reports the device attached.</param>
    /// <param name="CapturedFileName">An existing DCS filename for this physical instance.</param>
    public sealed record DetectedDcsDevice(Guid PhysicalGuid, string Name, bool Connected, string? CapturedFileName = null)
    {
        #region BEGIN Public
        /// <summary>Gets the captured filename or a native GUID-qualified default.</summary>
        public string FileName => CapturedFileName ?? $"{Name} {{{NativeGuid()}}}.diff.lua";
        /// <summary>Gets a physical-instance choice including its connection status.</summary>
        public string Title => FileName[..^9] + (Connected ? " · Connected" : " · Saved profile");
        #endregion END Public
        #region BEGIN private helpers
        private string NativeGuid()
        {
            var value = PhysicalGuid.ToString("D").ToUpperInvariant();
            return value[..14] + value[14..18].ToLowerInvariant() + value[18..];
        }
        #endregion END private helpers
    }
}

