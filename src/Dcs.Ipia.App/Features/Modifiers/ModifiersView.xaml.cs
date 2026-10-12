using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using System.Windows;
using System.Windows.Controls;

namespace DcsIpia.Features.Modifiers
{
    public partial class ModifiersView : UserControl
    {
        private IModuleWorkspaceContext? _context;
        private AssignmentLayer _layer = AssignmentLayer.Module;

        public ModifiersView()
        {
            InitializeComponent();
            ModifierModeChoice.ItemsSource = Enum.GetValues<ModifierMode>();
        }
        
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            _context.StatusChanged += (_, msg) => StatusMessage.Text = msg;
            ModifiersScope.SelectedIndex = 0;
            Refresh();
        }

        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_context is null || ModifiersScope.SelectedIndex < 0) return;
            _layer = ModifiersScope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
            Refresh();
        }

        private void Refresh()
        {
            if (_context?.Working is null)
            {
                StatusText.Text = "No working snapshot";
                ManagedModifiers.ItemsSource = null;
                return;
            }

            var state = WorkspaceStructureEditor.Describe(_context.Working);
            var modifiers = state.Modifiers.Where(x => x.Layer == _layer).ToArray();
            ManagedModifiers.ItemsSource = modifiers;
            ModifierDevice.ItemsSource = state.Devices.Where(x => x.Layer == _layer).ToArray();
            StatusText.Text = $"{modifiers.Length} modifiers in {_layer}";
        }

        private void ManagedModifierSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_context?.Working is null || ManagedModifiers.SelectedItem is not WorkspaceModifier modifier)
            {
                ModifierName.Text = "";
                ModifierSemantic.Text = "";
                ModifierModeChoice.SelectedItem = null;
                ModifierDevice.SelectedItem = null;
                ModifierControl.SelectedItem = null;
                return;
            }

            ModifierName.Text = modifier.Name;
            ModifierSemantic.Text = modifier.SemanticLayer;
            ModifierModeChoice.SelectedItem = modifier.Mode;

            var devices = WorkspaceStructureEditor.Describe(_context.Working).Devices
                .Where(x => x.Layer == _layer).ToArray();
            ModifierDevice.ItemsSource = devices;
            ModifierDevice.SelectedItem = devices.FirstOrDefault(x => x.ProfileId == modifier.ProfileId);

            var controls = _context.Working.Diagrams
                .FirstOrDefault(x => x.ProfileId == modifier.ProfileId)?.Controls
                .Where(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal))
                .DistinctBy(x => x.InputKey)
                .ToArray() ?? [];
            ModifierControl.ItemsSource = controls;
            ModifierControl.SelectedItem = controls.FirstOrDefault(x =>
                DcsInputKey.SamePhysicalInput(x.InputKey, modifier.Key));
        }

        private WorkspaceModifier ModifierCandidate()
        {
            var device = ModifierDevice.SelectedItem as WorkspaceDevice
                ?? throw new InvalidOperationException("Select a device.");
            var control = ModifierControl.SelectedItem as HardwareGraphicControl
                ?? throw new InvalidOperationException("Select a shared control.");
            var mode = ModifierModeChoice.SelectedItem is ModifierMode value
                ? value
                : throw new InvalidOperationException("Select Hold or Toggle.");
            return new(_layer, device.ProfileId, ModifierName.Text, control.InputKey, ModifierSemantic.Text, mode, "");
        }

        private void UpdateModifierClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedModifiers.SelectedItem is not WorkspaceModifier modifier)
            {
                StatusMessage.Text = "Select a modifier to update.";
                return;
            }

            try
            {
                var candidate = ModifierCandidate();
                _context.RunStructureEdit(workspace =>
                    WorkspaceStructureEditor.SetModifier(workspace, modifier, candidate));
                StatusMessage.Text = "Modifier updated and staged. Save to database to keep it, or Discard draft.";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = ex.Message;
            }
        }

        private void RemoveModifierClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedModifiers.SelectedItem is not WorkspaceModifier modifier)
            {
                StatusMessage.Text = "Select a modifier to remove.";
                return;
            }

            var before = ManagedModifiers.Items.Count;
            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.RemoveModifier(workspace, modifier));

            if (ManagedModifiers.Items.Count < before)
                StatusMessage.Text = "Modifier removal staged. Save to database to keep it, or Discard draft.";
            // otherwise StatusChanged already wrote the “used by N assignment(s)” message
        }

        private void NewModifierClicked(object sender, RoutedEventArgs e)
        {
            ManagedModifiers.SelectedItem = null;
            ModifierName.Text = "";
            ModifierSemantic.Text = "";
            ModifierModeChoice.SelectedItem = ModifierMode.Hold;
            ModifierDevice.SelectedIndex = 0;
            StatusMessage.Text = "Enter name, mode, device and control, then Add.";
        }

        private void AddModifierClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                var candidate = ModifierCandidate();
                _context.RunStructureEdit(workspace =>
                    WorkspaceStructureEditor.SetModifier(workspace, null, candidate));
                StatusMessage.Text = "Modifier added and staged. Save to database to keep it, or Discard draft.";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = ex.Message;
            }
        }

        private void ModifierDeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_context?.Working is null || ModifierDevice.SelectedItem is not WorkspaceDevice device)
            {
                ModifierControl.ItemsSource = null;
                return;
            }

            var controls = _context.Working.Diagrams
                .FirstOrDefault(x => x.ProfileId == device.ProfileId)?.Controls
                .Where(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal))
                .DistinctBy(x => x.InputKey)
                .ToArray() ?? [];

            ModifierControl.ItemsSource = controls;
            ModifierControl.SelectedIndex = controls.Length > 0 ? 0 : -1;
        }

    }
}