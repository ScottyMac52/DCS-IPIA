namespace Dcs.Ipia.Application;

/// <summary>A colored modifier entry on the combined device kneeboard.</summary>
/// <param name="Label">Modifier or UI Layer description.</param>
/// <param name="Color">Color applied to corresponding callout labels.</param>
public sealed record KneeboardModifierLegend(string Label,string Color);
