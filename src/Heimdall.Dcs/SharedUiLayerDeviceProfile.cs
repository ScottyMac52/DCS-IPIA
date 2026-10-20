namespace Heimdall.Dcs
{
    /// <summary>Describes one shared default profile and the references that protect its removal.</summary>
    /// <param name="Path">Canonical source path.</param>
    /// <param name="FileName">Exact native DCS filename.</param>
    /// <param name="Source">Captured Lua for stale-selection detection.</param>
    /// <param name="ActiveBindings">Active keys and axes.</param>
    /// <param name="ModifierCount">Modifiers activated by this device.</param>
    /// <param name="Protected">Whether this is a protected keyboard profile.</param>
    public sealed record SharedUiLayerDeviceProfile(string Path, string FileName, string Source,
        int ActiveBindings, int ModifierCount, bool Protected)
    {
        #region BEGIN Public
        #endregion END Public
    }
}
