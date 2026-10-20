namespace Heimdall.Application
{
    /// <summary>One labeled function and its source color on a combined kneeboard callout.</summary>
    /// <param name="Text">Displayed assignment label.</param>
    /// <param name="Color">DCS-Common modifier color.</param>
    /// <param name="Scope">Module controls or UI Layer.</param>
    /// <param name="ModifierChord">Modifier names or empty for Base.</param>
    public sealed record KneeboardLabelVariant(string Text, string Color, AssignmentLayer Scope, string ModifierChord);
}
