namespace Heimdall.Application
{
    /// <summary>Compares one module's pinned source with the opened defaults and proposed draft.</summary>
    /// <param name="Path">Exact shared source path.</param>
    /// <param name="Status">Matches baseline, Matches proposal, Different source or Not pinned.</param>
    public sealed record SharedUiLayerModuleSourceImpact(string Path, string Status);
}

