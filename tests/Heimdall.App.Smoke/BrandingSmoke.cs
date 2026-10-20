using System.Windows.Media.Imaging;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that the embedded brand assets decode from a compiled application without loose files.</summary>
    public static class BrandingSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies full artwork dimensions and every Windows icon size, including high-DPI variants.</summary>
        public static void Verify()
        {
            foreach (var filename in new[] { "heimdall.ico", "heimdall-monochrome.ico" })
            {
                var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/Heimdall;component/Branding/" + filename),
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var sizes = decoder.Frames.Select(frame => frame.PixelWidth).Order().ToArray();
                if (!sizes.SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }) ||
                    decoder.Frames.Any(frame => frame.PixelWidth != frame.PixelHeight))
                    throw new InvalidOperationException("HEIMDALL icon lacks required Windows sizes.");
            }
            if (ApplicationBranding.Icon.PixelWidth != 512 || ApplicationBranding.Icon.PixelHeight != 512 ||
                ApplicationBranding.Badge.PixelWidth != 512 || ApplicationBranding.Badge.PixelHeight != 512 ||
                ApplicationBranding.Banner.PixelWidth != 1200 || ApplicationBranding.Banner.PixelHeight != 300 ||
                !ApplicationBranding.Logo.IsFrozen || !ApplicationBranding.Monochrome.IsFrozen ||
                !ApplicationBranding.Icon.IsFrozen || !ApplicationBranding.Badge.IsFrozen || !ApplicationBranding.Banner.IsFrozen)
                throw new InvalidOperationException("Embedded HEIMDALL artwork is invalid.");
        }
        #endregion END Public
    }
}
