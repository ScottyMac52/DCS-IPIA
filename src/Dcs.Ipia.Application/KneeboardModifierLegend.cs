namespace Dcs.Ipia.Application
{
    /// <summary>A colored modifier entry shared by the combined kneeboard and live input test.</summary>
    /// <param name="Label">Human-readable modifier or UI Layer description.</param>
    /// <param name="Color">Exact kneeboard callout color.</param>
    /// <param name="Layer">Assignment scope of the entry.</param>
    /// <param name="Chord">Structured modifier chord; empty for the unmodified layer.</param>
    public sealed record KneeboardModifierLegend(
        string Label, string Color, AssignmentLayer Layer = AssignmentLayer.Module, string Chord = "");
}
