using Heimdall.Application;

namespace Heimdall.App.Games
{
    /// <summary>Adapter-supplied, typed hardware diagram commands. Null handlers denote
    /// unsupported functionality; menu items are never invented by the renderer.</summary>
    internal sealed record HardwareDiagramInteractions(
        Action<HardwareGraphicControl>? Select,
        Action<HardwareGraphicControl, string>? Drop = null,
        Func<HardwareGraphicControl, bool>? CanClear = null,
        Action<HardwareGraphicControl>? Clear = null,
        Func<HardwareGraphicControl, bool>? CanTune = null,
        Action<HardwareGraphicControl>? Tune = null,
        string DragFormat = "heimdall.elite.command",
        string ClearLabel = "Clear assignment",
        Action<HardwareGraphicControl>? Inspect = null,
        Func<HardwareGraphicControl, string, bool>? CanDrop = null);
}

