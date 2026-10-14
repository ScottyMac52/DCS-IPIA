using Dcs.Ipia.Application;

namespace DcsIpia.Features.InputTest
{
    /// <summary>Hardware artwork, physical hit regions, legend and original combined-label source.</summary>
    public sealed record InputTestDiagramModel(
        byte[] BackgroundPng, double Width, double Height,
        IReadOnlyList<InputTestCallout> Callouts,
        IReadOnlyList<KneeboardModifierLegend> Legend, KneeboardComposite Page);
}
