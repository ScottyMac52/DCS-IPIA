using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation.ViewModels
{
    public class ModifiersComponentViewModel : BaseViewModel
    {
        private readonly Func<EditorWorkspaceSnapshot?> _getWorkspace;
        private readonly Action<Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot>>? _runStructureEdit;

        public ModifiersComponentViewModel(Func<EditorWorkspaceSnapshot?> getWorkspace,
            Action<Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot>>? runStructureEdit = null)
        {
            _getWorkspace = getWorkspace ?? throw new ArgumentNullException(nameof(getWorkspace));
            _runStructureEdit = runStructureEdit;
            ManagedModifiers = new ObservableCollection<WorkspaceModifier>();
            Devices = new ObservableCollection<WorkspaceDevice>();
            Controls = new ObservableCollection<HardwareGraphicControl>();
            Modes = new ObservableCollection<ModifierMode>(Enum.GetValues<ModifierMode>());
            NewCommand = new DelegateCommand(NewModifier);
            AddCommand = new DelegateCommand(AddModifier);
            UpdateCommand = new DelegateCommand(UpdateModifier);
            RemoveCommand = new DelegateCommand(RemoveModifier);
            Refresh();
        }

        public ObservableCollection<WorkspaceModifier> ManagedModifiers { get; }
        public ObservableCollection<WorkspaceDevice> Devices { get; }
        public ObservableCollection<HardwareGraphicControl> Controls { get; }
        public ObservableCollection<ModifierMode> Modes { get; }

        private WorkspaceModifier? _selectedModifier;
        public WorkspaceModifier? SelectedModifier { get => _selectedModifier; set { SetProperty(ref _selectedModifier, value); PopulateFromSelected(); } }

        private WorkspaceDevice? _selectedDevice;
        public WorkspaceDevice? SelectedDevice { get => _selectedDevice; set { SetProperty(ref _selectedDevice, value); PopulateControls(); } }

        private HardwareGraphicControl? _selectedControl;
        public HardwareGraphicControl? SelectedControl { get => _selectedControl; set => SetProperty(ref _selectedControl, value); }

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _semantic = string.Empty;
        public string Semantic { get => _semantic; set => SetProperty(ref _semantic, value); }

        private ModifierMode _selectedMode = ModifierMode.Hold;
        public ModifierMode SelectedMode { get => _selectedMode; set => SetProperty(ref _selectedMode, value); }

        public ICommand NewCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand RemoveCommand { get; }

        public void Refresh()
        {
            ManagedModifiers.Clear();
            Devices.Clear();
            Controls.Clear();
            var workspace = _getWorkspace();
            if (workspace is null) return;
            var state = WorkspaceStructureEditor.Describe(workspace);
            foreach (var m in state.Modifiers) ManagedModifiers.Add(m);
            foreach (var d in state.Devices) Devices.Add(d);
        }

        private void PopulateControls()
        {
            Controls.Clear();
            var workspace = _getWorkspace();
            if (workspace is null || SelectedDevice is null) return;
            var diagram = workspace.Diagrams.FirstOrDefault(x => x.ProfileId == SelectedDevice.ProfileId);
            if (diagram is null) return;
            foreach (var c in diagram.Controls.Where(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal)).DistinctBy(x => x.InputKey)) Controls.Add(c);
            if (Controls.Count > 0) SelectedControl = Controls[0];
        }

        private void PopulateFromSelected()
        {
            if (SelectedModifier is null) { NewModifier(); return; }
            Name = SelectedModifier.Name;
            Semantic = SelectedModifier.SemanticLayer;
            SelectedMode = SelectedModifier.Mode;
            SelectedDevice = Devices.FirstOrDefault(x => x.ProfileId == SelectedModifier.ProfileId && x.Layer == SelectedModifier.Layer);
            // SelectedControl will be set by PopulateControls when SelectedDevice is set
        }

        private void NewModifier()
        {
            SelectedModifier = null;
            Name = string.Empty;
            Semantic = string.Empty;
            SelectedMode = ModifierMode.Hold;
            if (Devices.Count > 0) SelectedDevice = Devices[0];
        }

        private void AddModifier()
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            var device = SelectedDevice ?? throw new InvalidOperationException("Select a device.");
            var control = SelectedControl ?? throw new InvalidOperationException("Select a shared control.");
            var mode = SelectedMode;
            var modifier = new WorkspaceModifier(ModifierManagementLayer(device), device.ProfileId, Name, control.InputKey, Semantic, mode, "");
            _runStructureEdit(ws => WorkspaceStructureEditor.SetModifier(ws, null, modifier));
            Refresh();
        }

        private static AssignmentLayer ModifierManagementLayer(WorkspaceDevice device) => device.Layer;

        private void UpdateModifier()
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            var existing = SelectedModifier ?? throw new InvalidOperationException("Select a modifier to update.");
            var device = SelectedDevice ?? throw new InvalidOperationException("Select a device.");
            var control = SelectedControl ?? throw new InvalidOperationException("Select a shared control.");
            var mode = SelectedMode;
            var candidate = new WorkspaceModifier(ModifierManagementLayer(device), device.ProfileId, Name, control.InputKey, Semantic, mode, "");
            _runStructureEdit(ws => WorkspaceStructureEditor.SetModifier(ws, existing, candidate));
            Refresh();
        }

        private void RemoveModifier()
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            var selected = SelectedModifier ?? throw new InvalidOperationException("Select a modifier to remove.");
            _runStructureEdit(ws => WorkspaceStructureEditor.RemoveModifier(ws, selected));
            Refresh();
        }
    }
}
