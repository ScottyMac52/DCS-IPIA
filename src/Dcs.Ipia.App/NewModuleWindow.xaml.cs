using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DcsIpia.Presentation
{
    /// <summary>Creates a local module with reviewed physical units and a pinned default baseline.</summary>
    public partial class NewModuleWindow : Window
    {
        private readonly IEditorWorkspaceRepository _repository;
        private readonly Action? _completed;
        private readonly SharedUiLayerWorkspace _defaults;
        private readonly IReadOnlyDictionary<string, string> _hardwareSources;
        private readonly IConnectedDeviceDiscovery _discovery;
        private readonly ObservableCollection<NewModuleDeviceSelection> _selections = [];
        private IReadOnlyList<WorkspaceHardware> _hardware;
        private readonly HardwareInventoryManager? _inventory;
        private readonly ILogger<NewModuleWindow> _logger;
        private string _savedGamesRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        /// <summary>Initializes a creation form from verified internal assets.</summary>
        /// <param name="repository">Local module storage.</param>
        /// <param name="defaults">Saved shared defaults to pin.</param>
        /// <param name="hardwareSources">Pinned internal hardware choices.</param>
        /// <param name="inventory">Optional saved physical inventory.</param>
        /// <param name="discovery">Optional physical controller discovery adapter.</param>
        /// <param name="logger">Optional DI-configured diagnostic logger.</param>
        /// <param name="completed">Optional completion callback for an embedded form.</param>
        /// <param name="savedGamesRoot">Optional isolated discovery root.</param>
        public NewModuleWindow(IEditorWorkspaceRepository repository, SharedUiLayerWorkspace defaults,
            IReadOnlyDictionary<string, string> hardwareSources, IConnectedDeviceDiscovery? discovery = null, HardwareInventoryManager? inventory = null,
            ILogger<NewModuleWindow>? logger = null, Action? completed = null, string? savedGamesRoot = null)
        {
            _completed = completed;
            if (savedGamesRoot is not null) _savedGamesRoot = savedGamesRoot;
            _logger = logger ?? NullLogger<NewModuleWindow>.Instance;
            _inventory = inventory;
            _repository = repository;
            _defaults = defaults;
            _hardwareSources = hardwareSources;
            _discovery = discovery ?? new WindowsConnectedDeviceDiscovery();
            _hardware = WorkspaceStructureEditor.Hardware(new(
                new(new(Guid.NewGuid(), "catalog", "Hardware"), [], [], [], "internal catalog"),
                [], [], [], [], [], [], new Dictionary<string, string>(), hardwareSources));
            InitializeComponent();
            BaselineText.Text = $"Shared default UI Layer revision {defaults.Revision} will be copied into this module. Its filenames are baseline identities; select a copied profile below to explicitly bind it to your unit.";
            HardwareChoices.ItemsSource = _hardware;
            SelectedUnits.ItemsSource = _selections;
            DefaultChoiceChanged(this, new RoutedEventArgs());
            Loaded += async (_, _) => { await LoadInventoryAsync(); await RefreshDevicesAsync(); };
        }
        #region BEGIN Public
        /// <summary>Gets the successfully saved and reopened module, or null when cancelled.</summary>
        public EditorWorkspaceSnapshot? CreatedWorkspace { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private async Task LoadInventoryAsync()
        {
            if (_inventory is null) return;
            try
            {
                var choices = InventoryHardwareAuthoring.Choices(await _inventory.ReadSnapshotAsync());
                InventoryChoices.ItemsSource = choices;
                _hardware = _hardware.Concat(choices.Select(x => GenericHardwareLayout.Create(x.Definition)))
                    .DistinctBy(x => x.DeviceId).ToArray();
                HardwareChoices.ItemsSource = _hardware;
            }
            catch (Exception ex)
            {
                ApplicationLog.InventoryFailure(_logger, ex);
                CreationError.Text = "Unable to read inventory: " + ex.Message;
            }
        }
        private void DefaultChoiceChanged(object sender, RoutedEventArgs e)
        {
            if (BaselineText is null || BaselineChoices is null) return;
            var empty = EmptyDefaults.IsChecked == true;
            BaselineText.Text = empty ? "Empty baseline selected. No personal controller identities, assignments or modifiers will be inherited."
                : $"Shared default revision {_defaults.Revision} will be copied. Explicitly rebind inherited profiles to your units.";
            BaselineChoices.IsEnabled = !empty;
            if (empty) BaselineChoices.SelectedItem = null;
        }
        private void InventoryChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InventoryChoices.SelectedItem is not InventoryHardwareChoice choice) return;
            PhysicalChoices.SelectedItem = null;
            HardwareChoices.SelectedItem = _hardware.Single(x => x.DeviceId == "generic-" + choice.Definition.Id);
            NativeFileInput.Text = choice.FileName;
            UnitInstance.Text = choice.Instance.Label;
            UnitRole.Text = choice.Instance.Role;
            CreationError.Text = "Inventoried unit selected with its actual controls. Add it to the reviewed list; choose compatible bundled artwork explicitly if desired.";
        }
        private async Task RefreshDevicesAsync()
        {
            RefreshDevicesButton.IsEnabled = false;
            try
            {
                var errors = new List<string>();
                var choices = await Task.Run(() => {
                    IReadOnlyList<DetectedDcsDevice> connected = [];
                    IReadOnlyList<DetectedDcsDevice> captured = [];
                    try { connected = _discovery.Discover(); }
                    catch (Exception ex)
                    {
                        ApplicationLog.ControllerDiscoveryFailure(_logger, ex);
                        errors.Add("Controllers: " + ex.Message);
                    }
                    try { captured = SavedGamesDeviceDiscovery.Discover(_savedGamesRoot); }
                    catch (Exception ex)
                    {
                        ApplicationLog.CapturedDiscoveryFailure(_logger, _savedGamesRoot, ex);
                        errors.Add("Saved Games: " + ex.Message);
                    }
                    return DcsPhysicalIdentity.Merge(connected, captured);
                });
                if (_logger.IsEnabled(LogLevel.Debug))
                    ApplicationLog.DevicesDiscovered(_logger, choices.Count, _savedGamesRoot, null);
                PhysicalChoices.ItemsSource = choices;
                CreationError.Text = choices.Count + " physical unit(s). " + string.Join(" ", errors);
            }
            finally { RefreshDevicesButton.IsEnabled = true; }
        }
        private async void RefreshDevicesClicked(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();
        private async void SavedGamesClicked(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFolderDialog { Title = "Select Saved Games or your DCS folder" };
            if (picker.ShowDialog(this) != true) return;
            _savedGamesRoot = picker.FolderName;
            await RefreshDevicesAsync();
        }
        private void PhysicalChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PhysicalChoices.SelectedItem is not DetectedDcsDevice device) return;
            NativeFileInput.Text = device.FileName;
            var catalog = new EditorWorkspaceSnapshot(new(new(Guid.NewGuid(), "catalog", "Hardware"), [], [], [], "catalog"),
                [], [], [], [], [], [], new Dictionary<string, string>(), _hardwareSources);
            var match = DetectedDeviceHardwareMatcher.Match(catalog, device);
            HardwareChoices.SelectedItem = match is null ? null : _hardware.FirstOrDefault(x => x.DeviceId == match.DeviceId);
        }
        private void HardwareChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BaselineChoices is null) return;
            BaselineChoices.SelectedItem = null;
            if (HardwareChoices.SelectedItem is not WorkspaceHardware hardware)
            {
                BaselineChoices.ItemsSource = null;
                return;
            }
            var resolved = SharedUiLayerHardwareMapping.Resolve(_defaults, _hardware);
            BaselineChoices.ItemsSource = resolved.Where(x => x.Value.Path == hardware.Diagram.Path)
                .Select(x => System.IO.Path.GetFileName(x.Key)).ToArray();
        }
        private void ManualFileClicked(object sender, RoutedEventArgs e)
        {
            PhysicalChoices.SelectedItem = null;
            NativeFileInput.Focus();
            CreationError.Text = "Enter the offline unit's captured native filename, including its physical GUID.";
        }
        private void ClearBaselineClicked(object sender, RoutedEventArgs e) => BaselineChoices.SelectedItem = null;
        private void AddUnitClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var hardware = HardwareChoices.SelectedItem as WorkspaceHardware
                    ?? throw new InvalidOperationException("Select the shared hardware for this physical unit.");
                var file = NativeFileInput.Text.Trim();
                var guid = DcsPhysicalIdentity.FromFileName(file)
                    ?? throw new InvalidOperationException("Select a physical unit or enter its GUID-qualified native DCS filename.");
                if (PhysicalChoices.SelectedItem is DetectedDcsDevice detected && guid != detected.PhysicalGuid)
                    throw new InvalidOperationException("Keep the selected unit's physical GUID, or clear the physical choice before manual entry.");
                if (_selections.Any(x => DcsPhysicalIdentity.FromFileName(x.FileName) == guid))
                    throw new InvalidOperationException("That physical unit is already in the list. Select another unit of the same hardware type.");
                if (InventoryChoices.SelectedItem is InventoryHardwareChoice inventory && guid == inventory.Instance.PhysicalGuid &&
                    !hardware.Diagram.Controls.Select(x => x.InputKey).ToHashSet(StringComparer.Ordinal).SetEquals(inventory.Definition.Capabilities.DcsKeys))
                    throw new InvalidOperationException("The chosen hardware must match this inventoried unit's actual controls.");
                var baseline = EmptyDefaults.IsChecked == true ? null : BaselineChoices.SelectedItem as string;
                if (baseline is not null && _selections.Any(x => x.BaselineFileName == baseline))
                    throw new InvalidOperationException("That copied baseline has already been bound. Choose a new unit instead.");
                _selections.Add(new(hardware, file, UnitInstance.Text, UnitRole.Text, baseline));
                PhysicalChoices.SelectedItem = null;
                NativeFileInput.Text = UnitInstance.Text = UnitRole.Text = "";
                BaselineChoices.SelectedItem = null;
                CreationError.Text = "Unit added. Add another physical unit of the same hardware type if needed.";
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    ApplicationLog.AddUnitFailure(_logger,
                        NativeFileInput.Text, DcsPhysicalIdentity.FromFileName(NativeFileInput.Text),
                        (HardwareChoices.SelectedItem as WorkspaceHardware)?.DeviceId, BaselineChoices.SelectedItem as string, ex);
                CreationError.Text = ex.Message;
            }
        }
        private void RemoveUnitClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedUnits.SelectedItem is NewModuleDeviceSelection selected) _selections.Remove(selected);
        }
        private async void CreateClicked(object sender, RoutedEventArgs e)
        {
            CreateButton.IsEnabled = false;
            EditorWorkspaceSnapshot? stagedWorkspace = null;
            try
            {
                NewModuleWorkspaceFactory.ValidateIdentity(DcsIdInput.Text, DisplayNameInput.Text);
                if (!string.IsNullOrWhiteSpace(NativeFileInput.Text))
                    throw new InvalidOperationException("Add the selected unit to the list before creating the module, or clear its filename.");
                foreach (var id in await _repository.ListAsync())
                    if ((await _repository.GetAsync(id))?.Configuration.Module.DcsId.Equals(DcsIdInput.Text, StringComparison.OrdinalIgnoreCase) == true)
                        throw new InvalidOperationException("That DCS input folder already has a saved module. Open it or choose another identity.");
                var workspace = NewModuleWorkspaceFactory.Create(DcsIdInput.Text, DisplayNameInput.Text, _defaults, _hardwareSources, EmptyDefaults.IsChecked == true);
                stagedWorkspace = workspace;
                foreach (var selected in _selections)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        ApplicationLog.ApplyingUnit(_logger,
                            selected.FileName, DcsPhysicalIdentity.FromFileName(selected.FileName), selected.Hardware.DeviceId, selected.BaselineFileName, null);
                    workspace = NewModuleDeviceAuthoring.Apply(workspace, selected);
                    stagedWorkspace = workspace;
                }
                workspace = ModuleAxisCleanup.CaptureDevices(workspace,
                    (PhysicalChoices.ItemsSource as IEnumerable<DetectedDcsDevice> ?? []).Select(x => x.FileName)
                        .Concat(_selections.Select(x => x.FileName)));
                await _repository.SaveAsync(workspace);
                CreatedWorkspace = await _repository.GetAsync(workspace.Configuration.Module.Id)
                    ?? throw new System.IO.InvalidDataException("The new module could not be reopened from SQLite.");
                if (_completed is null) DialogResult = true;
                else _completed();
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    ApplicationLog.CreateModuleFailure(_logger,
                        DcsIdInput.Text, DisplayNameInput.Text, EmptyDefaults.IsChecked == true, _defaults.Revision,
                        _selections.Select(x => new { x.FileName, PhysicalGuid = DcsPhysicalIdentity.FromFileName(x.FileName),
                            HardwareId = x.Hardware.DeviceId, x.Instance, x.Role, x.BaselineFileName }).ToArray(),
                        stagedWorkspace?.Configuration.Profiles.Select(x => new { x.Id, x.DeviceId, x.SourceName,
                            PhysicalGuid = DcsPhysicalIdentity.FromFileName(x.SourceName) }).ToArray(), ex);
                CreationError.Text = ex.Message;
            }
            finally { CreateButton.IsEnabled = true; }
        }
        #endregion END private helpers
    }
}

