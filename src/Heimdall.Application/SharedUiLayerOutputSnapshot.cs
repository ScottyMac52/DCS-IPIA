namespace Heimdall.Application
{
    /// <summary>Portable shared defaults and the exact hardware graphics needed to reproduce their output.</summary>
    /// <param name="Defaults">Verified saved default revision.</param>
    /// <param name="HardwareSourceCommit">Pinned source identity of the hardware catalog.</param>
    /// <param name="Diagrams">Default profile paths mapped to pinned hardware graphics.</param>
    public sealed record SharedUiLayerOutputSnapshot(SharedUiLayerWorkspace Defaults, string HardwareSourceCommit,
        IReadOnlyDictionary<string, UiLayerDiagram> Diagrams);
}
