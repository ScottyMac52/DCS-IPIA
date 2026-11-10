using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Rendering;
using System.Text.Json;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that the installed application carries reusable Sol-R hardware without user imports.</summary>
    public static class SolRHardwareSmoke
    {
        private static readonly string[] sourceArray = ["JOY_RX", "JOY_RY", "JOY_Z", "JOY_SLIDER1", "JOY_SLIDER2"];

        #region BEGIN Public
        /// <summary>Loads the packaged catalog and renders every Sol-R grip layout.</summary>
        public static void Verify()
        {
            var sources = DcsModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root).GetAwaiter().GetResult();
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!
                .Where(x => x.DeviceId.StartsWith("tm-sol-r-ava-", StringComparison.Ordinal)).ToArray();
            if (hardware.Length != 4) throw new InvalidOperationException("The installed catalog must ship four Sol-R on AVA layouts.");
            new EditionPolicy().RequireHardware(hardware.Select(x => x.DeviceId));
            foreach (var layout in hardware)
            {
                var right = layout.DeviceId.StartsWith("tm-sol-r-ava-r-", StringComparison.Ordinal);
                if (layout.Diagram.CustomMapping is not null || layout.Diagram.Controls.Count != (right ? 37 : 32) ||
                    (right && sourceArray.Any(key => !layout.Diagram.Controls.Any(x => x.InputKey == key && x.IsAxis))) ||
                    !layout.Diagram.Controls.Any(x => x.InputKey == "JOY_RZ" && x.IsAxis) ||
                    CanonicalSvgRenderer.RenderComplete(layout.Diagram.Svg!, layout.Diagram.Width, layout.Diagram.Height).Length < 1000)
                    throw new InvalidOperationException("Sol-R built-in layout is incomplete: " + layout.DeviceId);
            }
            Console.WriteLine("Smoke: four installed shared Sol-R grip/AVA layouts render without a user drawing library.");
            var hornet = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!
                .Single(x => x.DeviceId == "ava-base-f18c");
            new EditionPolicy().RequireHardware([hornet.DeviceId]);
            if (hornet.Diagram.Path != "assets/shared/hardware/svg/grip-f18c.svg" ||
                hornet.Diagram.Controls.Count != 29 || hornet.Diagram.CustomMapping is not null ||
                !hornet.Diagram.Controls.Any(x => x.InputKey == "JOY_BTN1" && x.Label == "Trigger 1") ||
                !hornet.Diagram.Controls.Any(x => x.InputKey == "JOY_BTN6" && x.Label == "Trigger 2") ||
                CanonicalSvgRenderer.RenderComplete(hornet.Diagram.Svg!, hornet.Diagram.Width, hornet.Diagram.Height).Length < 1000)
                throw new InvalidOperationException("The installed AVA Hornet catalog must resolve to complete Hornet artwork.");
            Console.WriteLine("Smoke: installed AVA Hornet alias resolves and renders without a user drawing library.");
        }
        #endregion END Public
    }
}
