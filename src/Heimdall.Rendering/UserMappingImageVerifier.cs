using System.Xml.Linq;
using Heimdall.Application;
using SkiaSharp;

namespace Heimdall.Rendering
{
    /// <summary>Checks actual bounded raster decoding before custom source publication and kneeboard rendering.</summary>
    public static class UserMappingImageVerifier
    {
        #region BEGIN Public
        /// <summary>Rejects malformed encoded pixels rather than accepting a blank dropped image.</summary>
        /// <param name="revision">Verified source-derived mapping with embedded-only raster references.</param>
        public static void Validate(UserMappingRevision revision)
        {
            DrawioMappingImporter.Validate(revision);
            foreach (var image in XDocument.Parse(revision.Svg).Descendants().Where(x => x.Name.LocalName == "image"))
            {
                var reference = image.Attribute("href")!.Value;
                var bytes = Convert.FromBase64String(reference[(reference.IndexOf(',', StringComparison.Ordinal) + 1)..]);
                using var stream = new MemoryStream(bytes);
                using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException("Custom raster image cannot be decoded.");
                using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Custom raster pixels are malformed.");
            }
        }
        #endregion END Public
    }
}
