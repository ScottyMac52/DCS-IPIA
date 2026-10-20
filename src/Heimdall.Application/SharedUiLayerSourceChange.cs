namespace Heimdall.Application
{
    /// <summary>A source added, updated or removed in a proposed shared default revision.</summary>
    /// <param name="Path">Exact canonical shared source path.</param>
    /// <param name="Change">Added, Updated or Removed.</param>
    public sealed record SharedUiLayerSourceChange(string Path, string Change);
}

