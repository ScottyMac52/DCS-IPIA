namespace Dcs.Ipia.Domain;

/// <summary>Stores the source-ordered device recognition rules, key aliases, and instance patterns.</summary>
/// <param name="Rules">First-match filename rules in source order.</param>
/// <param name="InputKeyAliases">DCS input key aliases keyed by device identity.</param>
/// <param name="InstancePatterns">Instance matching patterns in source order.</param>
public sealed record SharedDeviceMap(IReadOnlyList<DeviceMappingRule> Rules,IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> InputKeyAliases,IReadOnlyList<DeviceInstancePattern> InstancePatterns);
