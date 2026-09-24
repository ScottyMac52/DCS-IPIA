using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Contains normalized shared Lua data with source provenance and diagnostics.</summary>
public sealed class SharedLuaImportResult {
 /// <summary>Initializes a shared Lua import result.</summary>
 /// <param name="controls">The physical hardware controls.</param>
 /// <param name="profiles">The canonical UI Layer profiles.</param>
 /// <param name="bindings">The parsed UI Layer bindings.</param>
 /// <param name="modifiers">The canonical UI Layer modifiers.</param>
 /// <param name="provenance">The source locations and hashes.</param>
 /// <param name="diagnostics">The validation diagnostics.</param>
 public SharedLuaImportResult(IReadOnlyList<SharedHardwareControl> controls,IReadOnlyList<DeviceProfile> profiles,IReadOnlyList<DcsBinding> bindings,IReadOnlyList<DcsModifier> modifiers,IReadOnlyList<SourceProvenance> provenance,IReadOnlyList<ImportDiagnostic> diagnostics) {
  Controls=controls;Profiles=profiles;Bindings=bindings;Modifiers=modifiers;Provenance=provenance;Diagnostics=diagnostics;
 }
 #region BEGIN Public
 /// <summary>Gets the hardware controls.</summary>
 public IReadOnlyList<SharedHardwareControl> Controls {get;}
 /// <summary>Gets the canonical UI Layer profiles.</summary>
 public IReadOnlyList<DeviceProfile> Profiles {get;}
 /// <summary>Gets the UI Layer bindings.</summary>
 public IReadOnlyList<DcsBinding> Bindings {get;}
 /// <summary>Gets the UI Layer modifiers.</summary>
 public IReadOnlyList<DcsModifier> Modifiers {get;}
 /// <summary>Gets the exact source provenance.</summary>
 public IReadOnlyList<SourceProvenance> Provenance {get;}
 /// <summary>Gets validation diagnostics.</summary>
 public IReadOnlyList<ImportDiagnostic> Diagnostics {get;}
 /// <summary>Gets whether every supplied source was imported successfully.</summary>
 public bool Succeeded=>!Diagnostics.Any(x=>x.IsError);
 #endregion END Public
}
