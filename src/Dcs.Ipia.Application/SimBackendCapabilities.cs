using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.Application
{
    /// <summary>Versioned declarations distinguish native filter support from portable curve approximation.</summary>
    /// <param name="Version">Backend contract version.</param>
    /// <param name="ButtonModes">Supported assignment modes; modifier switch modes remain native evidence.</param>
    /// <param name="ModifierChords">Whether button and axis chords are supported.</param>
    /// <param name="NativeAxisFilters">Whether exact backend-specific axis filters are preserved.</param>
    /// <param name="DeploysToGame">Whether this contract deploys output into active configuration.</param>
    public sealed record SimBackendCapabilities(int Version, IReadOnlyList<BindingMode> ButtonModes,
        bool ModifierChords, bool NativeAxisFilters, bool DeploysToGame);
}
