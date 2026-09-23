namespace Dcs.Ipia.Domain;

/// <summary>Preserves a canonical shared UI Layer overlay template before module-specific composition.</summary>
/// <param name="DeviceKey">The shared device identity.</param>
/// <param name="Status">The source template status.</param>
/// <param name="Modifier">The semantic modifier name.</param>
/// <param name="Instances">The source instance selectors.</param>
/// <param name="Bindings">Function identities mapped to graphical control identities.</param>
public sealed record UiLayerTemplate(string DeviceKey,string Status,string Modifier,IReadOnlyList<string> Instances,IReadOnlyDictionary<string,string> Bindings);
