using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Microsoft.Win32;

namespace DcsIpia.Presentation
{
    /// <summary>Creates a local module with reviewed physical units and a pinned default baseline.</summary>
    public partial class NewModuleWindow : Window
    {
        private readonly IEditorWorkspaceRepository _repository;
        private readonly SharedUiLayerWorkspace _defaults;
        private readonly IReadOnlyDictionary<string, string> _hardwareSources;
        private readonly IConnectedDeviceDiscovery _discovery;
        private readonly ObservableCollection<NewModuleDeviceSelection> _selections = [];
        private readonly IReadOnlyList<WorkspaceHardware> _hardware;
        private string _savedGamesRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        /// <summary>Initializes a creation form from verified internal assets.</summary>
        /// <param name="repository">Local module storage.</param>
        /// <param name="defaults">Saved shared defaults to pin.</param>
        /// <param name="hardwareSources">Pinned internal hardware choices.</param>
        /// <param name="discovery">Optional physical controller discovery adapter.</param>
        public NewModuleWindow(IEditorWorkspaceRepository repository, SharedUiLayerWorkspace defaults,
            IReadOnlyDictionary<string, string> hardwareSources, IConnectedDeviceDiscovery? discovery = null)
        {
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
            Loaded += async (_, _) => await RefreshDevicesAsync();
        }
        #region BEGIN Public
        /// <summary>Gets the successfully saved and reopened module, or null when cancelled.</summary>
        public EditorWorkspaceSnapshot? CreatedWorkspace { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
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
                    catch (Exception ex) { errors.Add("Controllers: " + ex.Message); }
                    try { captured = SavedGamesDeviceDiscovery.Discover(_savedGamesRoot); }
                    catch (Exception ex) { errors.Add("Saved Games: " + ex.Message); }
                    return DcsPhysicalIdentity.Merge(connected, captured);
                });
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
                var baseline = BaselineChoices.SelectedItem as string;
                if (baseline is not null && _selections.Any(x => x.BaselineFileName == baseline))
                    throw new InvalidOperationException("That copied baseline has already been bound. Choose a new unit instead.");
                _selections.Add(new(hardware, file, UnitInstance.Text, UnitRole.Text, baseline));
                PhysicalChoices.SelectedItem = null;
                NativeFileInput.Text = UnitInstance.Text = UnitRole.Text = "";
                BaselineChoices.SelectedItem = null;
                CreationError.Text = "Unit added. Add another physical unit of the same hardware type if needed.";
            }
            catch (Exception ex) { CreationError.Text = ex.Message; }
        }
        private void RemoveUnitClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedUnits.SelectedItem is NewModuleDeviceSelection selected) _selections.Remove(selected);
        }
        private async void CreateClicked(object sender, RoutedEventArgs e)
        {
            CreateButton.IsEnabled = false;
            try
            {
                NewModuleWorkspaceFactory.ValidateIdentity(DcsIdInput.Text, DisplayNameInput.Text);
                if (!string.IsNullOrWhiteSpace(NativeFileInput.Text))
                    throw new InvalidOperationException("Add the selected unit to the list before creating the module, or clear its filename.");
                foreach (var id in await _repository.ListAsync())
                    if ((await _repository.GetAsync(id))?.Configuration.Module.DcsId.Equals(DcsIdInput.Text, StringComparison.OrdinalIgnoreCase) == true)
                        throw new InvalidOperationException("That DCS input folder already has a saved module. Open it or choose another identity.");
                var workspace = NewModuleWorkspaceFactory.Create(DcsIdInput.Text, DisplayNameInput.Text, _defaults, _hardwareSources);
                foreach (var selected in _selections)
                    workspace = NewModuleDeviceAuthoring.Apply(workspace, selected);
                await _repository.SaveAsync(workspace);
                CreatedWorkspace = await _repository.GetAsync(workspace.Configuration.Module.Id)
                    ?? throw new System.IO.InvalidDataException("The new module could not be reopened from SQLite.");
                DialogResult = true;
            }
            catch (Exception ex) { CreationError.Text = ex.Message; }
            finally { CreateButton.IsEnabled = true; }
        }
        #endregion END private helpers
    }
}
