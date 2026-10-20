using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.Application
{
    /// <summary>An explicit instance resolution result; no product-name fallback is performed.</summary>
    /// <param name="Status">Resolved, disconnected, unresolved or ambiguous.</param>
    /// <param name="Matches">Observed exact instance matches.</param>
    public sealed record SimDeviceResolution(string Status, IReadOnlyList<PhysicalDeviceIdentity> Matches);
}
