using System.IO;
using System.Windows;
using Heimdall.Dcs;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.Rendering;
using SkiaSharp;

namespace Heimdall.App
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
            if (TrainingAssets.Scenarios().Count != 2) throw new InvalidDataException("Published training scenarios are unavailable.");
            var version = typeof(App).Assembly.GetName().Version!.ToString(4);
            if (window.Title != "HEIMDALL — Modules · version " + version)
                throw new InvalidDataException("Published application title differs from its four-part release version.");
            var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            if (hardware.Count == 0 || !window.IsVisible) throw new InvalidDataException("Published app did not load its window and hardware.");
            using var surface = SKSurface.Create(new SKImageInfo(4, 4)) ?? throw new InvalidDataException("Native graphics did not initialize.");
            surface.Canvas.Clear(SKColors.Cyan);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            if (png.Size == 0) throw new InvalidDataException("Published native PNG rendering failed.");
            if (!MainWindowCommands.SupportReport.CanExecute(null, window)) throw new InvalidDataException("Published support report command is unavailable.");
            if (!MainWindowCommands.UserMappings.CanExecute(null, window)) throw new InvalidDataException("Published custom mapping command is unavailable.");
            var capabilities = ControllerCapabilityMapper.Map([new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12, "Button 1")]);
            var definition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
            var source = """
                <mxfile><diagram><mxGraphModel pageWidth="900" pageHeight="600"><root>
                <mxCell id="0"/><mxCell id="1" parent="0"/>
                <mxCell id="canvas" parent="1" vertex="1" style="fillColor=#ffffff;"><mxGeometry width="900" height="600"/></mxCell>
                <mxCell id="label-JOY_BTN1" parent="1" vertex="1" value="Trigger"><mxGeometry x="10" y="100" width="300" height="50"/></mxCell>
                <mxCell id="footer" parent="1" vertex="1" value="Published custom mapping"><mxGeometry x="0" y="575" width="900" height="25"/></mxCell>
                </root></mxGraphModel></diagram></mxfile>
                """;
            source = source.Replace("</root>", "<mxCell id=\"hardware-image-controller\" parent=\"1\" vertex=\"1\" style=\"image=controller.png;\"><mxGeometry x=\"400\" y=\"100\" width=\"100\" height=\"100\"/></mxCell></root>", StringComparison.Ordinal);
            var revision = DrawioMappingImporter.Import(source, definition, "published.drawio", version,
                new Dictionary<string, byte[]> { ["controller.png"] = png.ToArray() });
            if (revision.Assets.Count != 1) throw new InvalidDataException("Published custom mapping did not capture its image asset.");
            var library = new FileUserMappingLibrary(folder);
            await library.SaveAsync(revision);
            var reopened = (await library.ListAsync()).Single();
            if (reopened.Id != revision.Id || CanonicalSvgRenderer.RenderComplete(reopened.Svg, reopened.Width, reopened.Height).Length == 0)
                throw new InvalidDataException("Published custom source persistence or native rendering failed.");
            await PublishedUserDataAcceptance.VerifyAsync(folder, reopened);
            File.WriteAllText(Path.Combine(folder, "release-smoke.ok"), "Visible application, isolated SQLite, bundled hardware and native PNG rendering verified.\n");
        }
        #endregion END Public
    }
}


