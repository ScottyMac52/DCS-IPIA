namespace Heimdall.WarThunder
{
    /// <summary>Explains inclusion, exclusion or missing evidence without substituting an enumeration position for a native game index.</summary>
    public sealed record WarThunderInputTrace(string InputId, int EnumerationIndex, string Kind, int? NativeIndex, string Detail);
}
