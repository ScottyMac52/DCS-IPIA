namespace Dcs.Ipia.Application
{
    /// <summary>Portable original artwork retained when a built-in device first adopts a user drawing.</summary>
    /// <param name="Path">Original pinned asset path.</param>
    /// <param name="Width">Original canvas width.</param>
    /// <param name="Height">Original canvas height.</param>
    /// <param name="Background">Original embedded raster bytes.</param>
    /// <param name="Svg">Original complete SVG.</param>
    /// <param name="Controls">Original stable controls and geometry.</param>
    public sealed record OriginalHardwareDrawing(string Path, double Width, double Height,
        byte[] Background, string Svg, IReadOnlyList<HardwareGraphicControl> Controls);
}
