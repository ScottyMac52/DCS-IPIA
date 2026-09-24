namespace Dcs.Ipia.Application;

/// <summary>Positions module-used UI Layer functions over a committed DCS-Common hardware image.</summary>
/// <param name="Device">Authored hardware label.</param>
/// <param name="Instance">Consumer device instance, if present.</param>
/// <param name="Path">Committed DCS-Common SVG source path.</param>
/// <param name="Width">SVG viewBox width.</param>
/// <param name="Height">SVG viewBox height.</param>
/// <param name="Background">The SVG's embedded PNG or JPEG hardware image.</param>
/// <param name="Callouts">Only assignments explicitly used by this module and device instance.</param>
public sealed record UiLayerDiagram(string Device,string? Instance,string Path,double Width,double Height,byte[] Background,IReadOnlyList<UiLayerCallout> Callouts) {
 #region BEGIN Public
 /// <summary>Gets the display name, including a physical instance when present.</summary>
 public string Title=>Instance is null?Device:$"{Device} · {Instance}";
 #endregion END Public
}
