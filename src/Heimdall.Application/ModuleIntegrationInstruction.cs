namespace Heimdall.Application
{
    /// <summary>Explicit module-owned instructions, independent of installed integration configuration.</summary>
    /// <param name="Name">Stable instruction group name.</param>
    /// <param name="Instructions">Captured operator guidance.</param>
    public sealed record ModuleIntegrationInstruction(string Name, string Instructions);
}
