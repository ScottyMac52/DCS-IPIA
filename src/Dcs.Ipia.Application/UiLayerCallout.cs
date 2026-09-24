namespace Dcs.Ipia.Application;

/// <summary>Locates an authored UI Layer assignment on a shared hardware diagram.</summary>
/// <param name="Number">Number shared by the diagram badge and accessible assignment list.</param>
/// <param name="Control">Authored graphic control identity.</param>
/// <param name="Function">Canonical function label.</param>
/// <param name="Modifier">Modifier chord from the consumer's explicit utilization.</param>
/// <param name="X">SVG box left.</param>
/// <param name="Y">SVG box top.</param>
/// <param name="Width">SVG box width.</param>
/// <param name="Height">SVG box height.</param>
public sealed record UiLayerCallout(int Number,string Control,string Function,string Modifier,double X,double Y,double Width,double Height) {
 #region BEGIN Public
 /// <summary>Gets an accessible, numbered description of the assignment.</summary>
 public string Description=>$"{Number:00} · {Function} · {Control} · {Modifier}";
 /// <summary>Gets the canonical DCS-Common command string for this function.</summary>
 public string CommandId {get;init;}=string.Empty;
 #endregion END Public
}
