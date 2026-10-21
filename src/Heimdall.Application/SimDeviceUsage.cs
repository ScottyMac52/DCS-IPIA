using Heimdall.Domain.MultiSim;

namespace Heimdall.Application
{
    /// <summary>One native device identity and all command fields that depend on it.</summary>
    /// <param name="DeviceId">Stable adapter reference identity.</param>
    /// <param name="Identity">Captured native device and optional index.</param>
    /// <param name="Assignments">Human-readable affected commands, fields and input keys.</param>
    public sealed record SimDeviceUsage(string DeviceId, PhysicalDeviceIdentity Identity, IReadOnlyList<string> Assignments);
}
