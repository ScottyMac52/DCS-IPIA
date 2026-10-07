namespace Dcs.Ipia.Application
{
    /// <summary>User-owned physical unit, stored separately from module profiles.</summary>
    /// <param name="PhysicalGuid">Exact DirectInput physical instance identity.</param>
    /// <param name="NativeName">Last reviewed native display name.</param>
    /// <param name="DefinitionId">Pinned reusable capability definition.</param>
    /// <param name="Label">User-defined distinguishable instance label.</param>
    /// <param name="Role">User-defined role.</param>
    /// <param name="Selected">Whether the user explicitly adopted this unit.</param>
    /// <param name="TemplateCandidates">Compatible artwork hints for this unit's reviewed identity, never automatically selected.</param>
    public sealed record HardwareInventoryInstance(Guid PhysicalGuid, string NativeName, string DefinitionId,
        string Label, string Role, bool Selected, IReadOnlyList<string> TemplateCandidates);
}
