using Dcs.Ipia.Application;

namespace DcsIpia.Features.InputTest
{
    /// <summary>Kneeboard-quality device image, independent transparent input regions and modifier legend.</summary>
    public sealed record InputTestDiagramModel(
        byte[] BackgroundPng,
        double Width,
        double Height,
        IReadOnlyList<InputTestCallout> Callouts,
        IReadOnlyList<KneeboardModifierLegend> Legend);
}
