namespace Heimdall.Application
{
    /// <summary>Reusable capability definition independent of physical and module identities.</summary>
    /// <param name="Id">Content-addressed definition ID.</param>
    /// <param name="Capabilities">Versioned capability evidence shared with input testing.</param>
    public sealed record HardwareDefinition(string Id, ControllerCapabilities Capabilities);
}
