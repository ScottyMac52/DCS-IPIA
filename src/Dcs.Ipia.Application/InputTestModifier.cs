namespace Dcs.Ipia.Application
{
    /// <summary>One effective generated modifier activator.</summary>
    /// <param name="PhysicalGuid">Captured test data.</param>
    /// <param name="Layer">Captured test data.</param>
    /// <param name="Name">Captured test data.</param>
    /// <param name="InputKey">Captured test data.</param>
    /// <param name="Mode">Captured test data.</param>
    public sealed record InputTestModifier(Guid PhysicalGuid, AssignmentLayer Layer, string Name, string InputKey, Dcs.Ipia.Domain.ModifierMode Mode);
}
