using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain;

/// <summary>Describes a DCS modifier input.</summary>
public sealed record DcsModifier {
 /// <summary>Initializes a new DCS modifier.</summary>
 /// <param name="name">The name.</param>
 /// <param name="device">The device.</param>
 /// <param name="key">The key.</param>
 /// <param name="mode">The mode.</param>
 public DcsModifier(string name,string device,string key,ModifierMode mode) {
  Name=Required(name,nameof(name)); Device=Required(device,nameof(device)); Key=Required(key,nameof(key)); Mode=mode;
 }
 #region BEGIN Public
 /// <summary>Gets the name.</summary>
 public string Name {get;}
 /// <summary>Gets the device.</summary>
 public string Device {get;}
 /// <summary>Gets the key.</summary>
 public string Key {get;}
 /// <summary>Gets the mode.</summary>
 public ModifierMode Mode {get;}
 #endregion END Public
 #region BEGIN private helpers
 private static string Required(string value,string name)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("A value is required.",name):value.Trim();
 #endregion END private helpers
}
