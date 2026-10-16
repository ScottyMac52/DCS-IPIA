using System.Windows.Media.Imaging;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks that the embedded brand assets decode from a compiled application without loose files.</summary>
    public static class BrandingSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies full artwork dimensions and every Windows icon size, including high-DPI variants.</summary>
        public static void Verify()
        {
            var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/Dcs.Ipia.App;component/Branding/flight-patch.ico"),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var sizes = decoder.Frames.Select(frame => frame.PixelWidth).Order().ToArray();
            if (!sizes.SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }) ||
                decoder.Frames.Any(frame => frame.PixelWidth != frame.PixelHeight))
                throw new InvalidOperationException("Flight Patch icon lacks required Windows sizes.");
            if (ApplicationBranding.Badge.PixelWidth != 512 || ApplicationBranding.Badge.PixelHeight != 512 ||
                ApplicationBranding.Banner.PixelWidth != 1200 || ApplicationBranding.Banner.PixelHeight != 300 ||
                !ApplicationBranding.Icon.IsFrozen || !ApplicationBranding.Badge.IsFrozen || !ApplicationBranding.Banner.IsFrozen)
                throw new InvalidOperationException("Embedded Flight Patch artwork is invalid.");
        }
        #endregion END Public
    }
}
