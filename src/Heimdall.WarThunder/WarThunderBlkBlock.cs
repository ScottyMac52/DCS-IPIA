namespace Heimdall.WarThunder
{
    /// <summary>A captured native block path and source offset; repeated block names remain distinct evidence.</summary>
    public sealed record WarThunderBlkBlock(string Path, int Start);
}
