using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain;

/// <summary>Validates a module's profiles, modifiers, and bindings.</summary>
public sealed record ModuleConfiguration {
 /// <summary>Initializes a new module configuration.</summary>
 /// <param name="module">The module.</param>
 /// <param name="profiles">The profiles.</param>
 /// <param name="modifiers">The modifiers.</param>
 /// <param name="bindings">The bindings.</param>
 /// <param name="provenance">The provenance.</param>
 public ModuleConfiguration(DcsModule module,IEnumerable<DeviceProfile> profiles,IEnumerable<DcsModifier> modifiers,IEnumerable<DcsBinding> bindings,string provenance) {
  Module=module??throw new ArgumentNullException(nameof(module)); Profiles=(profiles??throw new ArgumentNullException(nameof(profiles))).OrderBy(x=>x.DeviceId,StringComparer.Ordinal).ThenBy(x=>x.Instance??string.Empty,StringComparer.Ordinal).ToArray();
  Modifiers=(modifiers??throw new ArgumentNullException(nameof(modifiers))).OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray(); Bindings=(bindings??throw new ArgumentNullException(nameof(bindings))).OrderBy(x=>x.ProfileId).ThenBy(x=>x.Section).ThenBy(x=>x.CommandId,StringComparer.Ordinal).ThenBy(x=>x.Disposition).ThenBy(x=>x.InputKey,StringComparer.Ordinal).ToArray();
  EnsureUnique(Profiles.Select(x=>x.Id),"profile ID"); EnsureUnique(Profiles.Select(x=>$"{x.DeviceId}\0{x.Instance}"),"device instance",StringComparer.OrdinalIgnoreCase); EnsureUnique(Modifiers.Select(x=>x.Name),"modifier name",StringComparer.OrdinalIgnoreCase); EnsureUnique(Modifiers.Select(x=>$"{x.Device}\0{x.Key}"),"modifier input",StringComparer.OrdinalIgnoreCase);
  var profileIds=Profiles.Select(x=>x.Id).ToHashSet(); if(Bindings.Any(x=>!profileIds.Contains(x.ProfileId))) throw new ArgumentException("Every binding must reference a profile.",nameof(bindings));
  var modifierNames=Modifiers.Select(x=>x.Name).ToHashSet(StringComparer.Ordinal); if(Bindings.Where(x=>x.Disposition!=BindingDisposition.Removed).SelectMany(x=>x.Reformers).Any(x=>!modifierNames.Contains(x))) throw new ArgumentException("Every reformer must reference a declared modifier.",nameof(bindings));
  Provenance=string.IsNullOrWhiteSpace(provenance)?throw new ArgumentException("Provenance is required.",nameof(provenance)):provenance.Trim();
 }
 #region BEGIN Public
 /// <summary>Gets the module.</summary>
 public DcsModule Module {get;}
 /// <summary>Gets the profiles.</summary>
 public IReadOnlyList<DeviceProfile> Profiles {get;}
 /// <summary>Gets the modifiers.</summary>
 public IReadOnlyList<DcsModifier> Modifiers {get;}
 /// <summary>Gets the bindings.</summary>
 public IReadOnlyList<DcsBinding> Bindings {get;}
 /// <summary>Gets the provenance.</summary>
 public string Provenance {get;}
 #endregion END Public
 #region BEGIN private helpers
 private static void EnsureUnique<T>(IEnumerable<T> values,string label,IEqualityComparer<T>? comparer=null) {var all=values.ToArray();if(all.Distinct(comparer).Count()!=all.Length)throw new ArgumentException($"Duplicate {label}.");}
 #endregion END private helpers
}
