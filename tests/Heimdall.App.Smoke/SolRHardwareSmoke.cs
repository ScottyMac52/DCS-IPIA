using System;
using System.Linq;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Rendering;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that the installed application carries reusable Sol-R hardware without user imports.</summary>
    public static class SolRHardwareSmoke
    {
        #region BEGIN Public
        /// <summary>Loads the packaged catalog and renders every Sol-R grip layout.</summary>
        public static void Verify()
        {
            var sources = ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root).GetAwaiter().GetResult();
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!
                .Where(x => x.DeviceId.StartsWith("tm-sol-r-ava-", StringComparison.Ordinal)).ToArray();
            if (hardware.Length != 4) throw new InvalidOperationException("The installed catalog must ship four Sol-R on AVA layouts.");
            new EditionPolicy().RequireHardware(hardware.Select(x => x.DeviceId));
            foreach (var layout in hardware)
            {
                if (layout.Diagram.CustomMapping is not null || layout.Diagram.Controls.Count != 32 ||
                    !layout.Diagram.Controls.Any(x => x.InputKey == "JOY_RZ" && x.IsAxis) ||
                    CanonicalSvgRenderer.RenderComplete(layout.Diagram.Svg!, layout.Diagram.Width, layout.Diagram.Height).Length < 1000)
                    throw new InvalidOperationException("Sol-R built-in layout is incomplete: " + layout.DeviceId);
            }
            Console.WriteLine("Smoke: four installed shared Sol-R grip/AVA layouts render without a user drawing library.");
        }
        #endregion END Public
    }
}
