namespace Heimdall.Application
{
    /// <summary>Format-independent physical page choices; PNGs and PDFs use the same layout.</summary>
    /// <param name="Paper">A4 or Letter.</param>
    /// <param name="Landscape">Whether to exchange page width and height.</param>
    /// <param name="Artwork">Whether to prepend illustrated artwork to the complete assignment table.</param>
    public sealed record ReferenceSheetOptions(string Paper = "A4", bool Landscape = false, bool Artwork = false);
}
