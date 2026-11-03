namespace Heimdall.Application.Validation
{
    /// <summary>A generic half-open input-range contract and its independently selected native references.</summary>
    public sealed record ProfileValidationRange(string Path, string Offset, string Count, string References,
        string Index = "index", long? UnboundIndex = -1, long MaximumEnd = 2147483648);
}
