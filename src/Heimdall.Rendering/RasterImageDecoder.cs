using SkiaSharp;

namespace Heimdall.Rendering
{
    /// <summary>Decodes encoded artwork into owned raster pixels before native image drawing.</summary>
    internal static class RasterImageDecoder
    {
        #region BEGIN Public
        /// <summary>Validates encoded artwork and returns an image independent of the codec and bitmap lifetimes.</summary>
        /// <param name="encoded">The encoded image bytes.</param>
        /// <param name="errorMessage">The diagnostic for invalid or undecodable artwork.</param>
        /// <returns>An owned raster image that the caller must dispose.</returns>
        public static SKImage Decode(byte[] encoded, string errorMessage)
        {
            using var data = SKData.CreateCopy(encoded);
            using var codec = SKCodec.Create(data) ?? throw new InvalidDataException(errorMessage);
            using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException(errorMessage);
            return SKImage.FromBitmap(bitmap) ?? throw new InvalidDataException(errorMessage);
        }
        #endregion END Public
    }
}
