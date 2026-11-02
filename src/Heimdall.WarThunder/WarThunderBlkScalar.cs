namespace Heimdall.WarThunder
{
    /// <summary>A captured typed scalar with legacy path, original span and occurrence-aware native address.</summary>
    public sealed record WarThunderBlkScalar(string Path, char Type, string Value, int Start, int End)
    {
        #region BEGIN Public
        /// <summary>Gets the unique scalar occurrence address, independent of byte offsets.</summary>
        public string Address { get; init; } = "";
        /// <summary>Gets the containing block occurrence address.</summary>
        public string BlockAddress { get; init; } = "";
        #endregion END Public
    }
}
