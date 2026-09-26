using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Matches authored hardware POV keys to the physical keys used by DCS profiles.</summary>
public static class DcsInputKey {
 #region BEGIN Public
 /// <summary>Checks the known JOY_POV and JOY_BTN_POV spellings of the same physical input.</summary>
 /// <param name="first">Hardware or binding input key.</param>
 /// <param name="second">Hardware or binding input key.</param>
 /// <returns>Whether the keys identify the same input.</returns>
 public static bool SamePhysicalInput(string first,string second)=>
  string.Equals(ToDcsKey(first),ToDcsKey(second),StringComparison.Ordinal);

 /// <summary>Uses the profile's actual DCS key when an authored hardware key has another spelling.</summary>
 /// <param name="configuration">Stored module bindings.</param>
 /// <param name="profileId">Device profile identity.</param>
 /// <param name="section">Key or axis section.</param>
 /// <param name="hardwareKey">Key from the shared hardware definition.</param>
 /// <returns>The profile key, or the DCS POV spelling for an unassigned control.</returns>
 public static string Resolve(ModuleConfiguration configuration,Guid profileId,BindingSection section,string hardwareKey) {
  var actual=configuration.Bindings.Where(x=>x.ProfileId==profileId&&x.Section==section&&
   SamePhysicalInput(x.InputKey,hardwareKey)).Select(x=>x.InputKey).Distinct(StringComparer.Ordinal).ToArray();
  return actual.Length==1?actual[0]:ToDcsKey(hardwareKey);
 }
 #endregion END Public
 #region BEGIN private helpers
 private static string ToDcsKey(string key)=>key.StartsWith("JOY_POV",StringComparison.Ordinal)
  ?"JOY_BTN_POV"+key["JOY_POV".Length..]:key;
 #endregion END private helpers
}
