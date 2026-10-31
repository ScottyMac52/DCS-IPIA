namespace Heimdall.WarThunder
{
    /// <summary>A captured native scalar, preserving its path, type, literal value and original source span.</summary>
    public sealed record WarThunderBlkScalar(string Path, char Type, string Value, int Start, int End);
}
