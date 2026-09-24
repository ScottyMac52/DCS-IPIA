using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Stages module-owned assignments in memory with atomic conflicts, review, and undo.</summary>
public sealed class DraftAssignmentService {
 private readonly ModuleConfiguration _module;
 private readonly Dictionary<string,DraftCommand> _allowed;
 private readonly Dictionary<string,DraftAssignment> _original=new(StringComparer.Ordinal);
 private readonly Dictionary<string,DraftAssignment> _current=new(StringComparer.Ordinal);
 private readonly List<DraftOperation> _history=[];
 /// <summary>Starts a draft from explicitly imported assignments and source-scoped command catalogs.</summary>
 /// <param name="module">Module and its recognized profiles and modifiers.</param>
 /// <param name="commands">Permitted module and canonical UI Layer commands.</param>
 /// <param name="assignments">Validated existing assignments, including the module-local UI Layer overlay.</param>
 public DraftAssignmentService(ModuleConfiguration module,IEnumerable<DraftCommand> commands,IEnumerable<DraftAssignment> assignments) {
  _module=module??throw new ArgumentNullException(nameof(module));
  ArgumentNullException.ThrowIfNull(commands);
  ArgumentNullException.ThrowIfNull(assignments);
  _allowed=new Dictionary<string,DraftCommand>(StringComparer.Ordinal);
  foreach(var command in commands) {
   if(command is null||string.IsNullOrWhiteSpace(command.Id)||string.IsNullOrWhiteSpace(command.Name)||
      !Enum.IsDefined(command.Layer))throw new ArgumentException("Every catalog command needs an ID, name, and layer.",nameof(commands));
   if(!_allowed.TryAdd(CommandKey(command),command))throw new ArgumentException("Duplicate catalog command ID in one layer.",nameof(commands));
  }
  foreach(var assignment in assignments) {
   if(assignment is null)throw new ArgumentException("Initial assignment is missing.",nameof(assignments));
   if(!Validate(assignment.Slot,assignment.Command,out var reason))
    throw new ArgumentException(reason,nameof(assignments));
   if(!_original.TryAdd(assignment.Slot.Key,assignment))
    throw new ArgumentException("Two imported assignments occupy the same slot.",nameof(assignments));
   _current.Add(assignment.Slot.Key,assignment);
  }
 }
 #region BEGIN Public
 /// <summary>Gets the current draft assignments; no source is written by this service.</summary>
 public IReadOnlyList<DraftAssignment> Snapshot=>_current.Values.OrderBy(x=>x.Slot.Key,StringComparer.Ordinal).ToArray();
 /// <summary>Gets atomic before/after operations available for review and undo.</summary>
 public IReadOnlyList<DraftOperation> History=>_history.ToArray();
 /// <summary>Gets whether the draft differs from the imported assignments.</summary>
 public bool HasChanges=>_current.Count!=_original.Count||
  _current.Any(x=>!_original.TryGetValue(x.Key,out var original)||original!=x.Value);
 /// <summary>Stages a command on a selected control.</summary>
 /// <param name="destination">Target device, layer, input, and chord.</param>
 /// <param name="command">Command from the same validated catalog layer.</param>
 /// <param name="behavior">Saved occupied-destination preference.</param>
 /// <param name="choice">Explicit Ask decision, if already supplied.</param>
 /// <returns>Applied changes or a reason no edit was made.</returns>
 public DraftResult Assign(DraftAssignmentSlot destination,DraftCommand command,OccupiedDestinationBehavior behavior,DraftConflictChoice? choice=null) {
  if(!Validate(destination,command,out var reason))return Block(reason);
  _current.TryGetValue(destination.Key,out var existing);
  var incoming=new DraftAssignment(destination,command);
  if(existing==incoming)return Unchanged();
  var conflict=Conflict(existing,behavior,choice,false);
  if(conflict is not null)return conflict;
  return Commit("Assign",[new(destination,existing,incoming)]);
 }
 /// <summary>Moves a command between controls on the same device instance and layer.</summary>
 /// <param name="source">Occupied source control.</param>
 /// <param name="destination">Destination control.</param>
 /// <param name="behavior">Saved occupied-destination preference.</param>
 /// <param name="choice">Explicit Ask decision, including Swap.</param>
 /// <returns>Atomic before/after changes or a reason the move was blocked.</returns>
 public DraftResult Move(DraftAssignmentSlot source,DraftAssignmentSlot destination,OccupiedDestinationBehavior behavior,DraftConflictChoice? choice=null)=>
  Transfer(source,destination,behavior,choice,false);
 /// <summary>Copies a command without changing its source assignment.</summary>
 /// <param name="source">Occupied source control.</param>
 /// <param name="destination">Destination control on the same instance and layer.</param>
 /// <param name="behavior">Saved occupied-destination preference.</param>
 /// <param name="choice">Explicit Ask decision.</param>
 /// <returns>The staged copy or a reason no edit was made.</returns>
 public DraftResult Copy(DraftAssignmentSlot source,DraftAssignmentSlot destination,OccupiedDestinationBehavior behavior,DraftConflictChoice? choice=null)=>
  Transfer(source,destination,behavior,choice,true);
 /// <summary>Explicitly swaps two occupied controls on the same device instance and layer.</summary>
 /// <param name="source">First occupied control.</param>
 /// <param name="destination">Second occupied control.</param>
 /// <returns>One undoable operation or a blocked result.</returns>
 public DraftResult Swap(DraftAssignmentSlot source,DraftAssignmentSlot destination) {
  if(!SameContext(source,destination,out var reason))return Block(reason);
  if(source.Key==destination.Key)return Unchanged();
  if(!_current.TryGetValue(source.Key,out var first)||!_current.TryGetValue(destination.Key,out var second))
   return Block("Both controls must be assigned before swapping.");
  if(!Validate(destination,first.Command,out reason)||!Validate(source,second.Command,out reason))return Block(reason);
  return Commit("Swap",[new(source,first,new(source,second.Command)),new(destination,second,new(destination,first.Command))]);
 }
 /// <summary>Removes an assignment in the draft without modifying imported files.</summary>
 /// <param name="slot">Control to clear.</param>
 /// <returns>Before/after record or an unchanged result.</returns>
 public DraftResult Remove(DraftAssignmentSlot slot) {
  if(!ValidateSlot(slot,out var reason))return Block(reason);
  return _current.TryGetValue(slot.Key,out var prior)?Commit("Remove",[new(slot,prior,null)]):Unchanged();
 }
 /// <summary>Restores one slot to its imported value in an undoable operation.</summary>
 /// <param name="slot">Control to restore.</param>
 /// <returns>Before/after record or an unchanged result.</returns>
 public DraftResult Restore(DraftAssignmentSlot slot) {
  if(!ValidateSlot(slot,out var reason))return Block(reason);
  _current.TryGetValue(slot.Key,out var prior);
  _original.TryGetValue(slot.Key,out var imported);
  return prior==imported?Unchanged():Commit("Restore",[new(slot,prior,imported)]);
 }
 /// <summary>Reverses the last atomic operation, including both ends of a move or swap.</summary>
 /// <returns>The reversed operation or an unchanged result.</returns>
 public DraftResult Undo() {
  if(_history.Count==0)return Unchanged();
  var last=_history[^1];
  foreach(var change in last.Changes)Set(change.Slot.Key,change.Before);
  _history.RemoveAt(_history.Count-1);
  var reversed=last.Changes.Select(x=>new DraftChange(x.Slot,x.After,x.Before)).ToArray();
  return new(DraftOutcome.Applied,"Undid "+last.Name,new("Undo "+last.Name,Array.AsReadOnly(reversed)));
 }
 /// <summary>Discards the whole draft and restores the imported snapshot.</summary>
 public void Discard() {
  _current.Clear();
  foreach(var (key,value) in _original)_current.Add(key,value);
  _history.Clear();
 }
 #endregion END Public
 #region BEGIN private helpers
 private DraftResult Transfer(DraftAssignmentSlot source,DraftAssignmentSlot destination,OccupiedDestinationBehavior behavior,DraftConflictChoice? choice,bool copy) {
  if(!SameContext(source,destination,out var reason))return Block(reason);
  if(!_current.TryGetValue(source.Key,out var incoming))return Block("The source control has no assignment.");
  if(!Validate(destination,incoming.Command,out reason))return Block(reason);
  if(source.Key==destination.Key)return Unchanged();
  _current.TryGetValue(destination.Key,out var existing);
  var moved=new DraftAssignment(destination,incoming.Command);
  if(copy&&existing==moved)return Unchanged();
  var conflict=Conflict(existing,behavior,choice,!copy);
  if(conflict is not null)return conflict;
  if(existing is not null&&choice==DraftConflictChoice.Swap)
   return Commit("Swap",[new(source,incoming,new(source,existing.Command)),new(destination,existing,moved)]);
  return copy?Commit("Copy",[new(destination,existing,moved)]):
   Commit("Move",[new(source,incoming,null),new(destination,existing,moved)]);
 }
 private bool SameContext(DraftAssignmentSlot source,DraftAssignmentSlot destination,out string reason) {
  if(!ValidateSlot(source,out reason)||!ValidateSlot(destination,out reason))return false;
  if(source.ProfileId!=destination.ProfileId||source.Layer!=destination.Layer||source.Section!=destination.Section) {
   reason="A transfer must stay on the same device instance, layer, and key/axis section.";return false;
  }
  return true;
 }
 private bool Validate(DraftAssignmentSlot slot,DraftCommand command,out string reason) {
  if(!ValidateSlot(slot,out reason))return false;
  if(command is null||!_allowed.TryGetValue(CommandKey(command),out var allowed)||allowed!=command) {
   reason="Command is not in the validated catalog for this layer.";return false;
  }
  if(slot.Layer!=command.Layer||((slot.Section==BindingSection.Axis)!=command.IsAxis)) {
   reason="Command layer or key/axis type does not match the selected control.";return false;
  }
  return true;
 }
 private bool ValidateSlot(DraftAssignmentSlot slot,out string reason) {
  reason="";
  if(slot is null||slot.ModuleId!=_module.Module.Id||!_module.Profiles.Any(x=>x.Id==slot.ProfileId)||
     !Enum.IsDefined(slot.Layer)||!Enum.IsDefined(slot.Section)||string.IsNullOrWhiteSpace(slot.InputKey)) {
   reason="Control is outside the selected module, device profiles, or supported input sections.";return false;
  }
  if(slot.ModifierChord is null) {reason="Modifier chord is missing.";return false;}
  var names=slot.ModifierChord.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
  if((slot.ModifierChord.Length>0&&names.Length==0)||names.Distinct(StringComparer.Ordinal).Count()!=names.Length||
     names.Any(x=>!_module.Modifiers.Any(y=>y.Name==x))) {
   reason="Modifier chord is not declared by the selected module.";return false;
  }
  return true;
 }
 private static DraftResult? Conflict(DraftAssignment? existing,OccupiedDestinationBehavior behavior,DraftConflictChoice? choice,bool canSwap) {
  if(existing is null)return null;
  if(!Enum.IsDefined(behavior))return Block("Unknown occupied-destination preference.");
  if(behavior==OccupiedDestinationBehavior.Reject)return Block("Destination is occupied and replacement is disabled.");
  if(behavior==OccupiedDestinationBehavior.ReplaceInDraft)return null;
  if(choice is null)return new(DraftOutcome.ConfirmationRequired,"Destination is occupied; choose Replace, Swap, or Cancel.");
  if(choice==DraftConflictChoice.Cancel)return Block("Assignment canceled.");
  if(choice==DraftConflictChoice.Swap&&!canSwap)return Block("Swap requires moving an assigned control to another occupied control.");
  return choice is DraftConflictChoice.Replace or DraftConflictChoice.Swap?null:Block("Unknown occupied-destination choice.");
 }
 private DraftResult Commit(string name,DraftChange[] changes) {
  foreach(var change in changes)Set(change.Slot.Key,change.After);
  var operation=new DraftOperation(name,Array.AsReadOnly(changes));
  _history.Add(operation);
  return new(DraftOutcome.Applied,name+" staged.",operation);
 }
 private void Set(string key,DraftAssignment? value) {
  if(value is null)_current.Remove(key);else _current[key]=value;
 }
 private static string CommandKey(DraftCommand command)=>command.Layer+"\u001f"+command.Id;
 private static DraftResult Block(string? message)=>new(DraftOutcome.Blocked,message??"Invalid assignment.");
 private static DraftResult Unchanged()=>new(DraftOutcome.NoChange,"No draft change.");
 #endregion END private helpers
}
