using System.IO;
using System.Windows;
using Dcs.Ipia.Dcs;
using SkiaSharp;

namespace DcsIpia.Presentation
{
    /// <summary>Checks the actual portable or installed executable using an isolated local database.</summary>
    public static class PublishedApplicationSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies visible startup, shipped hardware assets and native graphics in the published process.</summary>
        /// <param name="window">Actual application main window.</param>
        /// <param name="folder">Isolated smoke database and result folder.</param>
        /// <returns>Completion after the application and its dependencies have initialized.</returns>
        public static async Task VerifyAsync(Window window, string folder)
        {
            BundledSharedCatalog.Validate(BundledSharedCatalog.Root);
            var version = typeof(App).Assembly.GetName().Version!.ToString(4);
            if (window.Title != "DCS-IPIA — Modules · version " + version)
                throw new InvalidDataException("Published application title differs from its four-part release version.");
            var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            if (hardware.Count == 0 || !window.IsVisible) throw new InvalidDataException("Published app did not load its window and hardware.");
            using var surface = SKSurface.Create(new SKImageInfo(4, 4)) ?? throw new InvalidDataException("Native graphics did not initialize.");
            surface.Canvas.Clear(SKColors.Cyan);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            if (png.Size == 0) throw new InvalidDataException("Published native PNG rendering failed.");
            File.WriteAllText(Path.Combine(folder, "release-smoke.ok"), "Visible application, isolated SQLite, bundled hardware and native PNG rendering verified.\n");
        }
        #endregion END Public
    }
}
