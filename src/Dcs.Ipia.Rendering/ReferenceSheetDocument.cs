namespace Dcs.Ipia.Rendering
{
    /// <summary>Matching page previews and a complete printable PDF produced by one drawing pipeline.</summary>
    /// <param name="PngPages">Individual 144-DPI page images in PDF page order.</param>
    /// <param name="Pdf">The complete controller reference document.</param>
    public sealed record ReferenceSheetDocument(IReadOnlyList<byte[]> PngPages, byte[] Pdf)
    {
        #region BEGIN Public
        /// <summary>Gets the number of preview and PDF pages.</summary>
        public int PageCount => PngPages.Count;
        #endregion END Public
    }
}
