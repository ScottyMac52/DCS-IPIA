namespace Heimdall.Squadrons
{
    /// <summary>Validated source kind and native index used by captured assignment filtering.</summary>
    internal readonly record struct SquadronsAssignmentSource(SquadronsAssignmentSourceKind Kind, int Index);
}
