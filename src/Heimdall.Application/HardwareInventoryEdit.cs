namespace Heimdall.Application
{
    /// <summary>Explicit reviewed inventory edit; changed capabilities require separate consent.</summary>
    /// <param name="Candidate">Current review evidence.</param>
    /// <param name="Selected">Adoption selection.</param>
    /// <param name="Label">User instance label.</param>
    /// <param name="Role">User role.</param>
    /// <param name="AdoptChangedCapabilities">Explicit adoption of a changed definition.</param>
    /// <param name="TemplateCandidates">Verified compatible bundled candidates for this evidence.</param>
    /// <param name="ReviewMapping">Whether the preferred mapping was explicitly reviewed during setup.</param>
    public sealed record HardwareInventoryEdit(HardwareInventoryCandidate Candidate, bool Selected, string Label,
        string Role, bool AdoptChangedCapabilities, IReadOnlyList<string> TemplateCandidates, bool ReviewMapping = false);
}
