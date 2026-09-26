using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Projects the current database draft into complete per-device kneeboard layers.</summary>
public static class KneeboardDraftProjection {
 #region BEGIN Public
 /// <summary>Gets every current scope and used modifier layer for one selected device.</summary>
 /// <param name="snapshot">Saved module and authored controls.</param>
 /// <param name="diagram">Selected hardware profile.</param>
 /// <param name="assignments">Current staged assignments, including unsaved changes.</param>
 /// <param name="labels">Current staged displayed labels.</param>
 /// <returns>Complete pages for the device's module and supported UI Layer scopes.</returns>
 public static IReadOnlyList<KneeboardDraftPage> Create(EditorWorkspaceSnapshot snapshot,UiLayerDiagram diagram,
  IEnumerable<DraftAssignment> assignments,IReadOnlyDictionary<string,string> labels) {
  ArgumentNullException.ThrowIfNull(snapshot);
  ArgumentNullException.ThrowIfNull(diagram);
  ArgumentNullException.ThrowIfNull(assignments);
  ArgumentNullException.ThrowIfNull(labels);
  var current=assignments.Where(x=>x.Slot.ModuleId==snapshot.Configuration.Module.Id&&
   x.Slot.ProfileId==diagram.ProfileId).ToArray();
  var bySlot=current.ToDictionary(x=>x.Slot.Key,x=>x,StringComparer.Ordinal);
  var pages=new List<KneeboardDraftPage>();
  foreach(var scope in diagram.SupportsUiLayer
   ?new[] {AssignmentLayer.Module,AssignmentLayer.UiLayer}:[AssignmentLayer.Module]) {
   var chords=current.Where(x=>x.Slot.Layer==scope).Select(x=>x.Slot.ModifierChord)
    .Where(x=>x.Length>0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
   foreach(var chord in Enumerable.Repeat("",1).Concat(chords)) {
    var captions=new Dictionary<string,string>(StringComparer.Ordinal);
    foreach(var control in diagram.Controls) {
     var slot=SlotForControl(snapshot,diagram,control,scope,chord);
     if(slot is null)continue;
     captions[control.Control]=bySlot.TryGetValue(slot.Key,out var assignment)
      ?labels.GetValueOrDefault(slot.Key)??assignment.Command.Name:"Unassigned";
    }
    pages.Add(new(scope,chord,captions));
   }
  }
  return pages;
 }
 /// <summary>Maps one graphic control to the exact assignment slot for a scope and chord.</summary>
 /// <param name="snapshot">Selected module configuration.</param>
 /// <param name="diagram">Authored device graphic.</param>
 /// <param name="control">Physical graphic control.</param>
 /// <param name="scope">Module or module UI Layer.</param>
 /// <param name="chord">Modifier chord; empty means Base.</param>
 /// <returns>The mapped slot, or null when the control does not appear on this layer.</returns>
 public static DraftAssignmentSlot? SlotForControl(EditorWorkspaceSnapshot snapshot,UiLayerDiagram diagram,
  HardwareGraphicControl control,AssignmentLayer scope,string chord) {
  ArgumentNullException.ThrowIfNull(snapshot);
  ArgumentNullException.ThrowIfNull(diagram);
  ArgumentNullException.ThrowIfNull(control);
  ArgumentNullException.ThrowIfNull(chord);
  if(scope==AssignmentLayer.UiLayer&&!diagram.SupportsUiLayer)return null;
  var shifted=control.Control.EndsWith("-shifted",StringComparison.Ordinal);
  if(shifted&&chord.Length==0)return null;
  var hasShiftedPair=!shifted&&diagram.Controls.Any(x=>x.Control==control.Control+"-shifted");
  if(scope==AssignmentLayer.UiLayer&&hasShiftedPair&&chord.Length>0)return null;
  var effectiveChord=hasShiftedPair?"":chord;
  var section=control.IsAxis?BindingSection.Axis:BindingSection.Key;
  var input=DcsInputKey.Resolve(snapshot.Configuration,diagram.ProfileId,section,control.InputKey);
  return new(snapshot.Configuration.Module.Id,diagram.ProfileId,scope,section,input,effectiveChord);
 }
 #endregion END Public
}
