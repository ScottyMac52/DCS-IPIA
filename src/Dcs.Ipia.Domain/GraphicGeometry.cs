namespace Dcs.Ipia.Domain
{
    /// <summary>Represents graphic geometry.</summary>
    /// <param name="X">The x.</param>
    /// <param name="Y">The y.</param>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    /// <param name="CoordinateSpace">The coordinate space.</param>
    public sealed record GraphicGeometry(double X, double Y, double Width, double Height, string CoordinateSpace);
}
