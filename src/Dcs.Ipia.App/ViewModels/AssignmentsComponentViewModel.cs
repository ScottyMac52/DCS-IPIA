using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation.ViewModels
{
    public class AssignmentsComponentViewModel : BaseViewModel
    {
        private readonly Func<EditorWorkspaceSnapshot?> _getWorkspace;
        private readonly Action<Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot>>? _runStructureEdit;

        public AssignmentsComponentViewModel(Func<EditorWorkspaceSnapshot?> getWorkspace,
            Action<Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot>>? runStructureEdit = null)
        {
            _getWorkspace = getWorkspace ?? throw new ArgumentNullException(nameof(getWorkspace));
            _runStructureEdit = runStructureEdit;
            Devices = new ObservableCollection<WorkspaceDevice>();
            Layers = new ObservableCollection<string> { "Base" };
        }

        public ObservableCollection<WorkspaceDevice> Devices { get; }
        public ObservableCollection<string> Layers { get; }

        private WorkspaceDevice? _selectedDevice;
        public WorkspaceDevice? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

        private string _selectedLayer = "Base";
        public string SelectedLayer { get => _selectedLayer; set => SetProperty(ref _selectedLayer, value); }

        public void Refresh()
        {
            Devices.Clear();
            Layers.Clear();
            Layers.Add("Base");
            var workspace = _getWorkspace();
            if (workspace is null) return;
            var state = WorkspaceStructureEditor.Describe(workspace);
            foreach (var d in state.Devices) Devices.Add(d);
            // include modifier names as layer choices where appropriate
            foreach (var name in state.Modifiers.Select(m => m.Name).Distinct()) Layers.Add(name);
        }

        public ICommand ClearLayerAssignmentsCommand => new DelegateCommand(() => {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            var workspace = _getWorkspace();
            if (workspace is null) return;
            var devices = WorkspaceStructureEditor.Describe(workspace).Devices;
            if (devices.Count == 0) throw new InvalidOperationException("No device available to clear.");
            var device = devices[0];
            var layer = SelectedLayer == "Base" ? "" : SelectedLayer;
            _runStructureEdit(ws => WorkspaceStructureEditor.ClearLayerAssignments(ws, device, layer));
        });

        // Lightweight delegation helpers so the window can pass domain operations into the component VM
        public void ApplyAssignment(Func<DraftConflictChoice?, DraftResult> operation, DraftAssignmentSlot? source, Action<Func<DraftConflictChoice?, DraftResult>, DraftAssignmentSlot?> apply)
        {
            // touch instance data to avoid CA1822 (method should remain instance-bound for incremental wiring)
            _ = _getWorkspace();
            apply(operation, source);
        }

        public void Undo(Action work)
        {
            _ = _getWorkspace();
            work();
        }

        public void ResetLabel(DraftAssignmentSlot? slot, Action work)
        {
            _ = _getWorkspace();
            work();
        }

        public void LabelChanged(Action work)
        {
            _ = _getWorkspace();
            work();
        }

        // New higher-level component operations
        public void AssignSlot(DraftAssignmentSlot slot, DraftAssignmentService draft, CommandPaletteEntry command, AssignmentLayer currentLayer, OccupiedDestinationBehavior behavior, Action<Func<DraftConflictChoice?, DraftResult>, DraftAssignmentSlot?> applyCallback)
        {
            ArgumentNullException.ThrowIfNull(draft);
            ArgumentNullException.ThrowIfNull(command);
            // touch instance data to avoid CA1822
            _ = _getWorkspace();
            // prepare operation
            Func<DraftConflictChoice?, DraftResult> op = choice =>
                draft.Assign(slot, new DraftCommand(command.Id, command.Name, command.IsAxis, currentLayer), behavior, choice);
            // execute via provided apply callback (keeps UI/dialog in window)
            applyCallback(op, slot);
        }

        public void RemoveSlot(DraftAssignmentSlot slot, DraftAssignmentService draft, Action<Func<DraftConflictChoice?, DraftResult>, DraftAssignmentSlot?> applyCallback)
        {
            ArgumentNullException.ThrowIfNull(draft);
            _ = _getWorkspace();
            Func<DraftConflictChoice?, DraftResult> op = _ => draft.Remove(slot);
            applyCallback(op, slot);
        }

        public DraftResult UndoDraft(DraftAssignmentService draft)
        {
            ArgumentNullException.ThrowIfNull(draft);
            _ = _getWorkspace();
            return draft.Undo();
        }

        public void ResetLabelSlot(DraftAssignmentSlot slot, Action onComplete)
        {
            _ = _getWorkspace();
            onComplete?.Invoke();
        }

        public void LabelChangedSlot(Action onChange)
        {
            _ = _getWorkspace();
            onChange?.Invoke();
        }

        // Label state owned by the assignments component
        private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, string> Labels => _labels;

        private bool _structureDirtyFlag;
        private DraftAssignmentService? _draftRef;
        public string PendingSummary => _structureDirtyFlag ? "Pending module authoring changes" : _draftRef?.HasChanges == true ? "Pending assignments" : "No pending assignment changes";

        public void SetDraftReference(DraftAssignmentService? draft) => _draftRef = draft;
        public void SetStructureDirty(bool value) => _structureDirtyFlag = value;

        public string? GetLabel(string key)
        {
            if (key is null) return null;
            return _labels.TryGetValue(key, out var v) ? v : null;
        }

        public void SetLabel(string key, string? value)
        {
            if (key is null) return;
            if (string.IsNullOrWhiteSpace(value))
                _labels.Remove(key);
            else
                _labels[key] = value!;
        }

        public void ReplaceLabels(IReadOnlyDictionary<string, string>? other)
        {
            _labels.Clear();
            if (other is null) return;
            foreach (var pair in other) _labels[pair.Key] = pair.Value;
        }

        public void ClearLabels() => _labels.Clear();

        // Inspector presentation properties
        private string _inspectorPhysicalInput = string.Empty;
        public string InspectorPhysicalInput { get => _inspectorPhysicalInput; set => SetProperty(ref _inspectorPhysicalInput, value); }

        private string _inspectorChord = string.Empty;
        public string InspectorChord { get => _inspectorChord; set => SetProperty(ref _inspectorChord, value); }

        private string _inspectorCurrentCommand = string.Empty;
        public string InspectorCurrentCommand { get => _inspectorCurrentCommand; set => SetProperty(ref _inspectorCurrentCommand, value); }

        private string _inspectorPendingCommand = string.Empty;
        public string InspectorPendingCommand { get => _inspectorPendingCommand; set => SetProperty(ref _inspectorPendingCommand, value); }

        private string _inspectorDefaultLabel = string.Empty;
        public string InspectorDefaultLabel { get => _inspectorDefaultLabel; set => SetProperty(ref _inspectorDefaultLabel, value); }

        private bool _isLabelEditorEnabled;
        public bool IsLabelEditorEnabled { get => _isLabelEditorEnabled; set => SetProperty(ref _isLabelEditorEnabled, value); }

        private string _labelEditorText = string.Empty;
        public string LabelEditorText { get => _labelEditorText; set => SetProperty(ref _labelEditorText, value); }

        // Update inspector view model fields from window context
        public void UpdateInspector(DraftAssignmentSlot? slot, DraftAssignment? assignment, HardwareGraphicControl? selected, EditorWorkspaceSnapshot? saved, UiLayerDiagram? currentDiagram)
        {
            InspectorPhysicalInput = selected is null ? "Select a callout" : selected.Label + " · " + selected.InputKey;
            InspectorChord = slot is null ? "" : slot.ModifierChord.Length == 0 ? "Base" : slot.ModifierChord;
            InspectorCurrentCommand = slot is null ? "" : saved?.Assignments.FirstOrDefault(x => DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot))?.Command.Name ?? "Unassigned";
            InspectorPendingCommand = assignment?.Command.Name ?? "Unassigned";
            InspectorDefaultLabel = selected?.Label ?? string.Empty;
            IsLabelEditorEnabled = assignment is not null;
            LabelEditorText = slot is null ? "" : assignment is null ? "Unassigned" : GetLabel(slot.Key) ?? assignment.Command.Name;
            // keep axis tuning and other specialized UI in the window for now
        }
    }
}
