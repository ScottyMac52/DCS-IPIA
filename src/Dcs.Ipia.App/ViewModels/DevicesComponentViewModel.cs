using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using System.Collections.Generic;
using System.Threading;

namespace DcsIpia.Presentation.ViewModels
{
    public class DevicesComponentViewModel : BaseViewModel
    {
        private readonly Func<EditorWorkspaceSnapshot?> _getWorkspace;
        private readonly Func<Task> _loadHardware;
        private readonly Action<string>? _setMessage;
        private readonly Action? _save;
        private readonly Action? _discard;

        private readonly Func<DraftAssignmentService?> _getDraft;
        private readonly Func<IReadOnlyDictionary<string, string>> _getLabels;

        private readonly Func<Task<IEnumerable<InventoryHardwareChoice>>>? _inventoryLoader;
        private readonly Func<Task<IEnumerable<UserMappingRevision>>>? _userMappingsLoader;
        private readonly Func<MappingRevisionReview, bool>? _mappingReviewConfirmation;
        private readonly Action<Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot>>? _runStructureEdit;

        public ObservableCollection<InventoryHardwareChoice> InventoryUnitsChoices { get; } = new();
        public ObservableCollection<UserMappingRevision> CustomMappingRevisionsChoices { get; } = new();

        private InventoryHardwareChoice? _selectedInventoryChoice;
        public InventoryHardwareChoice? SelectedInventoryChoice { get => _selectedInventoryChoice; set => SetProperty(ref _selectedInventoryChoice, value); }

        private UserMappingRevision? _selectedCustomMappingRevision;
        public UserMappingRevision? SelectedCustomMappingRevision { get => _selectedCustomMappingRevision; set => SetProperty(ref _selectedCustomMappingRevision, value); }

        public DevicesComponentViewModel(Func<EditorWorkspaceSnapshot?> getWorkspace,
                                         Func<Task> loadHardware,
                                         Func<DraftAssignmentService?> getDraft,
                                         Func<IReadOnlyDictionary<string, string>> getLabels,
                                         Func<Task<IEnumerable<InventoryHardwareChoice>>>? inventoryLoader = null,
                                         Func<Task<IEnumerable<UserMappingRevision>>>? userMappingsLoader = null,
                                         Func<MappingRevisionReview, bool>? mappingReviewConfirmation = null,
                                         Action<Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot>>? runStructureEdit = null,
                                         Action<string>? setMessage = null,
                                         Action? save = null,
                                         Action? discard = null)
        {
            _getWorkspace = getWorkspace ?? throw new ArgumentNullException(nameof(getWorkspace));
            _loadHardware = loadHardware ?? throw new ArgumentNullException(nameof(loadHardware));
            _getDraft = getDraft ?? throw new ArgumentNullException(nameof(getDraft));
            _getLabels = getLabels ?? throw new ArgumentNullException(nameof(getLabels));
            _inventoryLoader = inventoryLoader;
            _userMappingsLoader = userMappingsLoader;
            _mappingReviewConfirmation = mappingReviewConfirmation;
            _runStructureEdit = runStructureEdit;
            _setMessage = setMessage;
            _save = save; _discard = discard;
            ManagedDevices = new ObservableCollection<WorkspaceDevice>();
            LoadHardwareCommand = new DelegateCommand(() => _ = LoadHardwareAsync());
            SaveCommand = new DelegateCommand(() => _save?.Invoke());
            DiscardCommand = new DelegateCommand(() => _discard?.Invoke());
            SelectedScopeIndex = 0;
            Refresh();
        }

        // Device discovery / detected device handling
        public ObservableCollection<DetectedDcsDevice> DetectedDevicesChoices { get; } = new();

        private DetectedDcsDevice? _selectedDetectedDevice;
        public DetectedDcsDevice? SelectedDetectedDevice
        {
            get => _selectedDetectedDevice;
            set { if (SetProperty(ref _selectedDetectedDevice, value)) MatchSelectedDetectedDevice(); }
        }

        private string _detectedDevicesMessage = string.Empty;
        public string DetectedDevicesMessage { get => _detectedDevicesMessage; set => SetProperty(ref _detectedDevicesMessage, value); }

        private bool _isDiscoveryBusy;
        public bool IsDiscoveryBusy { get => _isDiscoveryBusy; set => SetProperty(ref _isDiscoveryBusy, value); }

        // When MatchSelectedDetectedDevice finds a candidate shared hardware match it records its device id here
        private string? _matchedDeviceId;
        public string? MatchedDeviceId { get => _matchedDeviceId; private set => SetProperty(ref _matchedDeviceId, value); }

        // Refresh detected devices with a loader that returns (choices, errors)
        public async Task RefreshDetectedDevicesAsync(Func<Task<(IReadOnlyList<DetectedDcsDevice> choices, string[] errors)>> loader)
        {
            if (IsDiscoveryBusy) return;
            IsDiscoveryBusy = true;
            try
            {
                DetectedDevicesMessage = "Reading connected controllers and Saved Games profiles…";
                string[] errors = Array.Empty<string>();
                IReadOnlyList<DetectedDcsDevice> choices = Array.Empty<DetectedDcsDevice>();
                try
                {
                    (choices, errors) = await loader();
                }
                catch (Exception ex)
                {
                    errors = new[] { ex.Message };
                    choices = Array.Empty<DetectedDcsDevice>();
                }
                DetectedDevicesChoices.Clear();
                foreach (var it in choices) DetectedDevicesChoices.Add(it);
                // preserve selection if possible
                SelectedDetectedDevice = DetectedDevicesChoices.FirstOrDefault(x => x.PhysicalGuid == SelectedDetectedDevice?.PhysicalGuid) ?? DetectedDevicesChoices.FirstOrDefault();

                // stage capture if it changed
                if (_getWorkspace is not null && _runStructureEdit is not null)
                {
                    var workspace = _getWorkspace();
                    if (workspace is not null && !ReferenceEquals(workspace, ModuleAxisCleanup.CaptureDevices(workspace, choices.Select(x => x.FileName))))
                    {
                        _runStructureEdit(ws => ModuleAxisCleanup.CaptureDevices(ws, choices.Select(x => x.FileName)));
                        DetectedDevicesMessage = "Controller identities for aircraft axis cleanup staged. Save to database to include them in generated inputs.";
                    }
                }

                DetectedDevicesMessage = (DetectedDevicesChoices.Count) + " physical device(s). Select a device, then verify shared hardware. " + string.Join(" ", errors);
            }
            finally
            {
                IsDiscoveryBusy = false;
            }
        }

        public void MatchSelectedDetectedDevice()
        {
            if (SelectedDetectedDevice is null || _getWorkspace is null) return;
            // If a managed device is actively selected, indicate selection and return
            if (SelectedManagedDevice is WorkspaceDevice)
            {
                ManagedFileName = SelectedDetectedDevice.FileName;
                DetectedDevicesMessage = "Physical unit selected. Use Rebind physical unit to review and stage the change for both scopes.";
                MatchedDeviceId = null;
                return;
            }
            var workspace = _getWorkspace();
            if (workspace is null) return;
            var match = DetectedDeviceHardwareMatcher.Match(workspace, SelectedDetectedDevice!);
            MatchedDeviceId = match?.DeviceId;
            ManagedFileName = SelectedDetectedDevice.FileName;
            DetectedDevicesMessage = match is null
                ? workspace.Sources?.ContainsKey("config/ipia-hardware.json") != true
                    ? "Physical GUID found, but the shared hardware catalog is missing. Click Load built-in hardware."
                    : "Physical GUID found. Select the matching shared hardware; the installed grip may require your choice."
                : "Physical GUID found. Shared hardware recognized; filename remains editable.";
            // MFD label fields enabled when candidate is a module-layer MFD
            KneeboardTitleFieldsEnabled = false; // conservative default
            MfdLabelFieldsEnabled = SelectedManagedDevice is not null && SelectedManagedDevice.Layer == AssignmentLayer.Module && match?.DeviceId == "tm-mfd";
        }

        public void ManualDeviceFileAction()
        {
            SelectedDetectedDevice = null;
            DetectedDevicesMessage = "Enter the unit's exact captured native filename, including its physical GUID.";
        }

        public void NewDeviceClicked()
        {
            SelectedManagedDevice = null;
            SelectedDetectedDevice = null;
            ManagedInstance = string.Empty;
            ManagedRole = string.Empty;
            ManagedKneeboardTitle = string.Empty;
            ManagedKneeboardSubtitle = string.Empty;
            KneeboardTitleFieldsEnabled = CurrentLayer == AssignmentLayer.Module;
            ManagedFileName = SelectedDetectedDevice?.FileName ?? string.Empty;
            MfdLeft = MfdRight = MfdTop = MfdBottom = string.Empty;
            MfdLabelFieldsEnabled = false;
        }

        public string NewDeviceFileName()
        {
            var guid = DcsPhysicalIdentity.FromFileName((ManagedFileName ?? string.Empty).Trim());
            if (guid is null) throw new InvalidOperationException("Select a detected device, or enter its GUID-qualified DCS filename.");
            if (SelectedDetectedDevice is DetectedDcsDevice selected && guid != selected.PhysicalGuid)
                throw new InvalidOperationException("Keep the selected physical device GUID in the filename.");
            return (ManagedFileName ?? string.Empty).Trim();
        }

        public async Task LoadInventoryChoicesAsync()
        {
            if (_inventoryLoader is null) return;
            try
            {
                InventoryUnitsChoices.Clear();
                var items = await _inventoryLoader();
                foreach (var it in items) InventoryUnitsChoices.Add(it);
            }
            catch (Exception ex)
            {
                Message = "Unable to read hardware inventory: " + ex.Message;
            }
        }

        public async Task LoadUserMappingsAsync()
        {
            if (_userMappingsLoader is null) return;
            try
            {
                CustomMappingRevisionsChoices.Clear();
                var items = await _userMappingsLoader();
                foreach (var it in items) CustomMappingRevisionsChoices.Add(it);
            }
            catch (Exception ex)
            {
                Message = "Unable to read user mappings: " + ex.Message;
            }
        }

        public void HandleManagedHardwareChanged(WorkspaceHardware? selectedHardware, string? detectedFileName, WorkspaceDevice? selectedDevice, AssignmentLayer deviceManagementLayer)
        {
            if (selectedDevice is null || selectedHardware is null)
            {
                ManagedFileName = detectedFileName ?? string.Empty;
                MfdLabelFieldsEnabled = false;
                return;
            }
            var profile = _getWorkspace()?.Configuration.Profiles.FirstOrDefault(x => x.Id == selectedDevice.ProfileId);
            if (profile is null) ManagedFileName = detectedFileName ?? string.Empty;
            else
            {
                if (profile.DeviceId != selectedHardware.DeviceId)
                    ManagedFileName = detectedFileName ?? string.Empty;
            }
            MfdLabelFieldsEnabled = deviceManagementLayer == AssignmentLayer.Module && selectedHardware.DeviceId == "tm-mfd";
        }

        public void AddInventory()
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            if (SelectedInventoryChoice is null) throw new InvalidOperationException("Select an adopted inventory unit. Discover and save it from the Hardware menu first.");
            _runStructureEdit(workspace => InventoryHardwareAuthoring.Add(workspace, SelectedInventoryChoice, SelectedScopeIndex == 0 ? AssignmentLayer.Module : AssignmentLayer.UiLayer));
        }

        public void AdoptCustomMapping()
        {
            if (_getWorkspace() is null || SelectedManagedDevice is null || SelectedCustomMappingRevision is null)
                { Message = "Select a physical profile and a saved drawing revision."; return; }
            try
            {
                var workspace = _getWorkspace()!;
                var revision = SelectedCustomMappingRevision;
                var device = SelectedManagedDevice;
                var review = MappingRevisionReview.Create(workspace.Diagrams.Single(x => x.ProfileId == device.ProfileId), revision);
                if (review.Conflicts.Count > 0) { Message = string.Join("\n", review.Conflicts); return; }
                var confirmed = _mappingReviewConfirmation?.Invoke(review) ?? false;
                if (!confirmed) return;
                if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
                _runStructureEdit(ws => UserMappingAdoption.Apply(ws, device.ProfileId, revision));
            }
            catch (Exception ex) { Message = "Unable to review drawing: " + ex.Message; }
        }

        // Device lifecycle operations migrated from ModuleWorkspaceWindow
        public void AddDevice(WorkspaceHardware hardware, string fileName, string? instance, string? role, string? kneeboardTitle, string? kneeboardSubtitle, AssignmentLayer layer)
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            _runStructureEdit(workspace => WorkspaceStructureEditor.AddDevice(workspace, hardware, layer, fileName, instance, role, kneeboardTitle, kneeboardSubtitle));
            Refresh();
        }

        public void UpdateDevice(WorkspaceDevice device, string fileName, string? instance, string? role, IReadOnlyDictionary<string, string>? mfdLabels, WorkspaceHardware? hardware, string? kneeboardTitle, string? kneeboardSubtitle)
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            _runStructureEdit(workspace => WorkspaceStructureEditor.UpdateDevice(workspace, device, fileName, instance, role, mfdLabels, hardware, kneeboardTitle, kneeboardSubtitle));
            Refresh();
        }

        public void RebindDevice(WorkspaceDevice device, string newFile, string instance, string role)
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            _runStructureEdit(workspace => WorkspaceStructureEditor.RebindDevice(workspace, device, newFile, instance, role));
            Refresh();
        }

        public void RemoveDevice(WorkspaceDevice device)
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            _runStructureEdit(workspace => WorkspaceStructureEditor.RemoveDevice(workspace, device));
            Refresh();
        }

        public void ClearDeviceAssignments(WorkspaceDevice device)
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            _runStructureEdit(workspace => WorkspaceStructureEditor.ClearDeviceAssignments(workspace, device));
            Refresh();
        }

        public void ClearLayerAssignments(Guid? profileId, string layerName, AssignmentLayer currentLayer)
        {
            if (_runStructureEdit is null) throw new InvalidOperationException("RunStructureEdit delegate not provided.");
            _runStructureEdit(workspace => {
                var device = WorkspaceStructureEditor.Describe(workspace).Devices.FirstOrDefault(x => x.ProfileId == profileId && x.Layer == currentLayer)
                             ?? throw new InvalidOperationException("Select a device to clear.");
                var layer = layerName is string s && s != "Base" ? s : "";
                return WorkspaceStructureEditor.ClearLayerAssignments(workspace, device, layer);
            });
            Refresh();
        }

        // Properties populated when a device is selected
        private string _managedFileName = string.Empty;
        public string ManagedFileName { get => _managedFileName; set => SetProperty(ref _managedFileName, value); }

        private string _managedInstance = string.Empty;
        public string ManagedInstance { get => _managedInstance; set => SetProperty(ref _managedInstance, value); }

        private string _managedRole = string.Empty;
        public string ManagedRole { get => _managedRole; set => SetProperty(ref _managedRole, value); }

        private string _managedKneeboardTitle = string.Empty;
        public string ManagedKneeboardTitle { get => _managedKneeboardTitle; set => SetProperty(ref _managedKneeboardTitle, value); }

        private string _managedKneeboardSubtitle = string.Empty;
        public string ManagedKneeboardSubtitle { get => _managedKneeboardSubtitle; set => SetProperty(ref _managedKneeboardSubtitle, value); }

        private string _mfdLeft = string.Empty;
        public string MfdLeft { get => _mfdLeft; set => SetProperty(ref _mfdLeft, value); }

        private string _mfdRight = string.Empty;
        public string MfdRight { get => _mfdRight; set => SetProperty(ref _mfdRight, value); }

        private string _mfdTop = string.Empty;
        public string MfdTop { get => _mfdTop; set => SetProperty(ref _mfdTop, value); }

        private string _mfdBottom = string.Empty;
        public string MfdBottom { get => _mfdBottom; set => SetProperty(ref _mfdBottom, value); }

        private bool _kneeboardTitleFieldsEnabled;
        public bool KneeboardTitleFieldsEnabled { get => _kneeboardTitleFieldsEnabled; set => SetProperty(ref _kneeboardTitleFieldsEnabled, value); }

        private bool _mfdLabelFieldsEnabled;
        public bool MfdLabelFieldsEnabled { get => _mfdLabelFieldsEnabled; set => SetProperty(ref _mfdLabelFieldsEnabled, value); }

        public void OnDeviceSelected(WorkspaceDevice device)
        {
            if (device is null) return;
            var workspace = _getWorkspace();
            if (workspace is null) return;
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            // set hardware choice - leave UI control binding to view if necessary
            ManagedFileName = device.FileName;
            ManagedInstance = device.Instance ?? string.Empty;
            ManagedRole = device.Role ?? string.Empty;
            var diagram = workspace.Diagrams.Single(x => x.ProfileId == device.ProfileId);
            var page = KneeboardDraftProjection.CreateCombined(workspace, diagram, _getDraft()?.Snapshot ?? workspace.Assignments, _getLabels());
            ManagedKneeboardTitle = page.Title;
            ManagedKneeboardSubtitle = page.Kicker;
            KneeboardTitleFieldsEnabled = (device.Layer == AssignmentLayer.Module);
            var labels = page.CategoryLabels;
            MfdLeft = labels.GetValueOrDefault("mfd-category-left") ?? string.Empty;
            MfdRight = labels.GetValueOrDefault("mfd-category-right") ?? string.Empty;
            MfdTop = labels.GetValueOrDefault("mfd-category-top") ?? string.Empty;
            MfdBottom = labels.GetValueOrDefault("mfd-category-bottom") ?? string.Empty;
            MfdLabelFieldsEnabled = device.Layer == AssignmentLayer.Module && profile.DeviceId == "tm-mfd";
        }

        public ObservableCollection<WorkspaceDevice> ManagedDevices { get; }

        private WorkspaceDevice? _selectedManagedDevice;
        public WorkspaceDevice? SelectedManagedDevice { get => _selectedManagedDevice; set => SetProperty(ref _selectedManagedDevice, value); }

        private int _selectedScopeIndex;
        public int SelectedScopeIndex
        {
            get => _selectedScopeIndex;
            set
            {
                if (SetProperty(ref _selectedScopeIndex, value)) Refresh();
            }
        }

        public ICommand LoadHardwareCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DiscardCommand { get; }

        private string _message = string.Empty;
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        private AssignmentLayer CurrentLayer => SelectedScopeIndex == 0 ? AssignmentLayer.Module : AssignmentLayer.UiLayer;

        public void Refresh()
        {
            ManagedDevices.Clear();
            var workspace = _getWorkspace();
            if (workspace is null) return;
            var devices = WorkspaceStructureEditor.Describe(workspace).Devices.Where(d => d.Layer == CurrentLayer).ToArray();
            foreach (var d in devices) ManagedDevices.Add(d);
        }

        private async Task LoadHardwareAsync()
        {
            try
            {
                Message = "Loading committed shared hardware…";
                _setMessage?.Invoke(Message);
                await _loadHardware();
                Refresh();
                Message = string.Empty;
                _setMessage?.Invoke(Message);
            }
            catch (Exception ex)
            {
                Message = "Shared hardware could not be loaded: " + ex.Message;
                _setMessage?.Invoke(Message);
            }
        }
    }
}
