using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Identifies one input and modifier chord on one module-owned device instance.</summary>
/// <param name="ModuleId">Owning module.</param>
/// <param name="ProfileId">Device instance profile.</param>
/// <param name="Layer">Aircraft or module-local UI Layer.</param>
/// <param name="Section">Key or axis.</param>
/// <param name="InputKey">Canonical physical input key.</param>
/// <param name="ModifierChord">Explicit modifier names separated by plus signs; empty means Base.</param>
public sealed record DraftAssignmentSlot(Guid ModuleId,Guid ProfileId,AssignmentLayer Layer,BindingSection Section,string InputKey,string ModifierChord="") {
 #region BEGIN Public
 /// <summary>Gets a stable value identity for a slot, including scope, instance, and chord.</summary>
 public string Key => string.Join("\u001f",ModuleId,ProfileId,Layer,Section,InputKey,ModifierChord);
 #endregion END Public
}
