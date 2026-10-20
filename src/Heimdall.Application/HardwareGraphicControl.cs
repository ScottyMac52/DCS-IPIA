namespace Heimdall.Application
{
    /// <summary>Connects an authored SVG callout to its canonical DCS input key.</summary>
    /// <param name="Control">Stable SVG control marker.</param>
    /// <param name="Label">Authored callout label such as OSB13.</param>
    /// <param name="InputKey">DCS input from the shared hardware Lua definition.</param>
    /// <param name="X">SVG left coordinate.</param>
    /// <param name="Y">SVG top coordinate.</param>
    /// <param name="Width">SVG callout width.</param>
    /// <param name="Height">SVG callout height.</param>
    /// <param name="IsAxis">Whether the authored hardware control is an axis.</param>
    public sealed record HardwareGraphicControl(string Control, string Label, string InputKey, double X, double Y,
                                                double Width, double Height, bool IsAxis = false);
}
