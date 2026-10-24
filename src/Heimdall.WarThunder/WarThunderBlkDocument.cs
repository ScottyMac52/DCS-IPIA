namespace Heimdall.WarThunder;

/// <summary>Lossless, read-only storage for War Thunder BLK profiles. Device IDs and activation codes must be verified before interpretation.</summary>
public sealed class WarThunderBlkDocument
{
    private readonly string _raw;
    private WarThunderBlkDocument(string raw) => _raw = raw;
    public static WarThunderBlkDocument Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return new WarThunderBlkDocument(raw);
    }
    public string SerializeUnchanged() => _raw;
}
