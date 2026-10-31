namespace Heimdall.WarThunder
{
    /// <summary>A captured block retains its occurrence identity and complete original source span.</summary>
    public sealed record WarThunderBlkBlock(string Path, int Start)
    {
        #region BEGIN Public
        /// <summary>Gets the unique block occurrence address.</summary>
        public string Address { get; init; } = "";
        /// <summary>Gets the parent block address, or empty for a root block.</summary>
        public string ParentAddress { get; init; } = "";
        /// <summary>Gets the original header start before the opening brace.</summary>
        public int HeaderStart { get; init; }
        /// <summary>Gets the source offset after the closing brace.</summary>
        public int End { get; init; }
        #endregion END Public
    }
}
