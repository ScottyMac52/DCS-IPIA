namespace DcsIpia.Features.InputTest
{
    /// <summary>Carries a rendered device background and live input callouts.</summary>
    /// <param name="BackgroundPng">Rendered device background bytes.</param>
    /// <param name="Width">Diagram width.</param>
    /// <param name="Height">Diagram height.</param>
    /// <param name="Callouts">Positioned assignment layer slices.</param>
    public sealed record InputTestDiagramModel(
        byte[] BackgroundPng,
        double Width,
        double Height,
        IReadOnlyList<InputTestCallout> Callouts);
}
