namespace Heimdall.Application
{
    /// <summary>A selected command source, without a machine-specific filesystem path.</summary>
    /// <param name="FileName">Selected file's basename.</param>
    /// <param name="Content">Original JSON or DCS HTML text.</param>
    public sealed record DcsCommandCatalogImportSource(string FileName, string Content);
}
