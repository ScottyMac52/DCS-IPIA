namespace Dcs.Ipia.Application
{
    /// <summary>A proposed baseline source change with module conflict evidence.</summary>
    /// <param name="Path">Canonical shared source path.</param>
    /// <param name="Change">Added, Updated or Removed.</param>
    /// <param name="Conflict">True when the module source differs from both baseline and proposal.</param>
    public sealed record SharedUiLayerAdoptionChange(string Path, string Change, bool Conflict);
}

