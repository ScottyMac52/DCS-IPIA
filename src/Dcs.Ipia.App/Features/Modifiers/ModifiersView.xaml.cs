using DcsIpia.Presentation;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using System.Windows;
using System.Windows.Controls;

namespace DcsIpia.Features.Modifiers
{
    /// <summary>Edits module and UI Layer modifier definitions.</summary>
    public partial class ModifiersView : UserControl
    {
        private IModuleWorkspaceContext? _context;
        private AssignmentLayer CurrentLayer => ModifiersScope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;

        #region BEGIN Public
        public ModifiersView()
        {
            InitializeComponent();
            ModifierModeChoice.ItemsSource = Enum.GetValues<ModifierMode>();
        }
        
        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            _context.StatusChanged += (_, msg) => StatusMessage.Text = msg;
            ModifiersScope.SelectedIndex = 0;
            Refresh();
        }
        #endregion END Public

        #region BEGIN private helpers
        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_context is null || ModifiersScope.SelectedIndex < 0) return;
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
            var modifiers = state.Modifiers.Where(x => x.Layer == CurrentLayer).ToArray();
            ManagedModifiers.ItemsSource = modifiers;
            ModifierDevice.ItemsSource = state.Devices.Where(x => x.Layer == CurrentLayer).ToArray();
            StatusText.Text = $"{modifiers.Length} modifiers in {CurrentLayer}";
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
                .Where(x => x.Layer == CurrentLayer).ToArray();
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
            return new(CurrentLayer, device.ProfileId, ModifierName.Text, control.InputKey, ModifierSemantic.Text, mode, "");
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

        private void ClearAssignmentsClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;
            var selected = ManagedModifiers.SelectedItem as WorkspaceModifier;
            var scope = CurrentLayer;
            var message = selected is null
                ? $"Clear all assignments in the {scope} scope?"
                : $"Clear assignments for modifier \"{selected.Name}\" in the {scope} scope?";
            if (ThemeMessageBox.Show(Window.GetWindow(this), message, "Clear assignments",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _context.RunStructureEdit(workspace =>
            {
                var devices = WorkspaceStructureEditor.Describe(workspace).Devices
                    .Where(d => d.Layer == scope);
                var current = workspace;
                if (selected is null)
                {
                    foreach (var device in devices)
                        current = WorkspaceStructureEditor.ClearLayerAssignments(current, device, "");
                    foreach (var mod in WorkspaceStructureEditor.Describe(current).Modifiers.Where(m => m.Layer == scope))
                        foreach (var device in devices)
                            current = WorkspaceStructureEditor.ClearLayerAssignments(current, device, mod.Name);
                }
                else
                {
                    foreach (var device in devices)
                        current = WorkspaceStructureEditor.ClearLayerAssignments(current, device, selected.Name);
                }
                return current;
            });
            StatusMessage.Text = "Assignments cleared and staged.";
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                await _context.SaveAsync();
                StatusMessage.Text = "Saved to database.";
            }
            catch (Exception ex) { StatusMessage.Text = "Database save failed: " + ex.Message; }
        }

        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _context?.Discard();
            StatusMessage.Text = "Pending edits discarded.";
        }
        #endregion END private helpers
    }
}
