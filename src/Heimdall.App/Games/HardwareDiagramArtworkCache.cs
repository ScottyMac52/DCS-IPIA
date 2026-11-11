using System.IO;
using System.Windows.Media.Imaging;
using Heimdall.Application.Games;
using Heimdall.Rendering;

namespace Heimdall.App.Games
{
    /// <summary>Retains frozen controller artwork independently of live callouts and menus.</summary>
    internal sealed class HardwareDiagramArtworkCache(int capacity = 8, bool preserveLabels = true,
        long pixelBudget = long.MaxValue)
    {
        private readonly Dictionary<(string Svg, double Width, double Height), BitmapImage> _images = [];
        private long _pixelBytes;

        #region BEGIN Public
        /// <summary>Reuses artwork only when the prepared SVG and output dimensions match.</summary>
        internal BitmapImage GetOrRender(string svg, double width, double height)
        {
            var key = (svg, width, height);
            if (_images.TryGetValue(key, out var image)) return image;
#if PERFORMANCE_TRACE
            using var raster = PerformanceTrace.Measure("diagram.rasterize", GetType());
#endif
            var png = preserveLabels ? CanonicalSvgRenderer.RenderComplete(svg, width, height)
                : CanonicalSvgRenderer.Render(svg, width, height);
            image = Decode(png);
            var bytes = PixelBytes(image);
            if (bytes > pixelBudget) return image;
            while (_images.Count >= capacity || _pixelBytes + bytes > pixelBudget)
            {
                var entry = _images.First();
                _pixelBytes -= PixelBytes(entry.Value);
                _images.Remove(entry.Key);
            }
            _images.Add(key, image);
            _pixelBytes += bytes;
            return image;
        }

        /// <summary>Decodes a frozen image that does not hold the input stream open.</summary>
        internal static BitmapImage Decode(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        #endregion END Public

        #region BEGIN private helpers
        private static long PixelBytes(BitmapImage image) => (long)image.PixelWidth * image.PixelHeight * 4;
        #endregion END private helpers
    }
}
