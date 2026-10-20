namespace Heimdall.App.Features.InputTest
{
    /// <summary>Identifies one actual physical control without duplicating its authored callout labels.</summary>
    /// <param name="InputKey">Native physical input used by the live input session.</param>
    /// <param name="ControlId">Authored SVG control identifier for resolving applicable modifier layers.</param>
    /// <param name="X">Left coordinate.</param>
    /// <param name="Y">Top coordinate.</param>
    /// <param name="Width">Control width.</param>
    /// <param name="Height">Control height.</param>
    public sealed record InputTestCallout(
        string InputKey, string ControlId, double X, double Y, double Width, double Height);
}
