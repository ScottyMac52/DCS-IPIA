using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Coordinates the assignments feature of the composed War Thunder workspace.</summary>
    public partial class WarThunderWorkspaceWindow
    {
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void WireAssignments()
        {
            var browser = _assignments.Browser;
            _assignments.ShowAssignments.Click += (_, _) => { browser.CommandsList.SelectedItem = null; _assignments.CapturedAssignments.IsExpanded = true; RefreshRecords(); };
            browser.Configure([new("All", null), new("Buttons", CommandType.Button), new("Axes", CommandType.Axis)],
                [new("All", GameCommandAssignmentStatus.All), new("Assigned to selected mapping", GameCommandAssignmentStatus.AssignedToController),
                 new("Not assigned to selected mapping", GameCommandAssignmentStatus.NotAssignedToController),
                 new("Assigned anywhere", GameCommandAssignmentStatus.AssignedAnywhere), new("Unassigned everywhere", GameCommandAssignmentStatus.UnassignedEverywhere)]);
            browser.CommandCategoryPicker.ItemsSource = new[] { "All categories", "Native hotkeys", "Native axes" };
            browser.CommandCategoryPicker.SelectedIndex = 0; browser.CommandTypePicker.SelectedIndex = 0; browser.AssignmentStatusPicker.SelectedIndex = 0;
            browser.CommandSearch.TextChanged += (_, _) => { if (!_refreshing) RefreshCommands(); };
            browser.CommandCategoryPicker.SelectionChanged += (_, _) => { if (!_refreshing) RefreshCommands(); };
            browser.CommandTypePicker.SelectionChanged += (_, _) => { if (!_refreshing) RefreshCommands(); };
            browser.AssignmentStatusPicker.SelectionChanged += (_, _) => { if (!_refreshing) RefreshCommands(); };
            browser.ClearCommandFiltersButton.Click += (_, _) =>
            {
                _refreshing = true; browser.CommandSearch.Clear(); browser.CommandCategoryPicker.SelectedIndex = 0;
                browser.CommandTypePicker.SelectedIndex = 0; browser.AssignmentStatusPicker.SelectedIndex = 0; _refreshing = false; RefreshCommands();
            };
            browser.CommandsList.SelectionChanged += (_, _) => { if (!_refreshing) { _selectedPhysicalInput = null; _selection.SelectCommand((browser.CommandsList.SelectedItem as SimCommand)?.Id); RefreshRecords(); } };
            _assignments.Records.SelectionChanged += (_, _) => { if (!_refreshing) { _selectedPhysicalInput = null; _selection.SelectInput(null); _selection.SelectAlternative((_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.RecordId); RefreshEditor(); } };
            _assignments.Editor.PhysicalApplyRequested += (_, _) => Run(() =>
            {
                var command = (_assignments.Browser.CommandsList.SelectedItem as SimCommand) ?? (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.Command;
                if (command is not null && _selectedPhysicalInput is not null) ApplyPhysicalAssignment(command.Id, _selectedPhysicalInput);
            });
            _assignments.Editor.EditRequested += (_, edit) => Run(() => ApplyDrawingEdit(edit));
            _assignments.Start.Click += (_, _) => Run(() =>
            {
                var pin = SelectedDevice is { } id ? _session?.Snapshot.Hardware.GetValueOrDefault(id) : null;
                if (pin is not null) _input.Start(pin, _observed);
            });
            _assignments.Stop.Click += (_, _) => _input.Stop();
            _assignments.Fit.Click += (_, _) => _viewport.Fit();
            var menu = new ContextMenu();
            var clear = new MenuItem { Header = "Clear assignment" };
            clear.Click += (_, _) => _assignments.Editor.Submit(WarThunderEditKind.Clear);
            _assignments.Editor.Tune.Click += (_, _) => TuneSelectedNativeAxis();
            var tune = new MenuItem { Header = "Tune axis…" };
            tune.Click += (_, _) => TuneSelectedNativeAxis();
            menu.Items.Add(clear); menu.Items.Add(tune); _assignments.Records.ContextMenu = menu;
            _assignments.Records.PreviewMouseRightButtonDown += (_, args) =>
            {
                var row = args.OriginalSource is DependencyObject origin ? ItemsControl.ContainerFromElement(_assignments.Records, origin) as DataGridRow : null;
                _selectedPhysicalInput = null; _selection.SelectInput(null);
                if (row is not null) row.IsSelected = true; else _assignments.Records.SelectedItem = null;
                RefreshEditor();
            };
            _assignments.Records.ContextMenuOpening += (_, args) =>
            {
                AssignmentActionPresentation.Show(clear, _assignments.Editor.ActionState(WorkspaceAssignmentAction.Clear));
                AssignmentActionPresentation.Show(tune, _assignments.Editor.ActionState(WorkspaceAssignmentAction.TuneAxis));
                if (clear.Visibility != Visibility.Visible && tune.Visibility != Visibility.Visible) args.Handled = true;
            };
        }
        private void RefreshCommands()
        {
            var browser = _assignments.Browser;
            var scope = (_session?.Snapshot.Id.ToString("N") ?? "") + ":" + SelectedDevice + ":" + browser.CommandCategoryPicker.SelectedItem + ":" + browser.CommandTypePicker.SelectedIndex;
            if (_selection.Scope != scope) _selectedPhysicalInput = null;
            _selection.SelectScope(scope);
            var selected = (browser.CommandsList.SelectedItem as SimCommand)?.Id;
            var catalog = _session?.Catalog.Commands ?? [];
            var owned = _rows.Where(row => SelectedDevice is null || row.DeviceIds.Contains(SelectedDevice)).ToArray();
            _assignments.AssignmentCount.Text = owned.Count(row => row.DeviceIds.Count > 0) + " captured joystick assignments · " + _rows.Count + " native records";
            var commands = WarThunderWorkspaceProjection.Filter(catalog, _rows, SelectedDevice, browser.CommandSearch.Text,
                browser.CommandCategoryPicker.SelectedIndex > 0 ? browser.CommandCategoryPicker.SelectedItem as string : null,
                (browser.CommandTypePicker.SelectedItem as GameCommandTypeOption)?.Type,
                (browser.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Status ?? GameCommandAssignmentStatus.All);
            _refreshing = true;
            try
            {
                _selection.Refresh(SelectedDevice, commands, selected);
                browser.CommandsList.ItemsSource = commands;
                browser.CommandsList.SelectedItem = commands.FirstOrDefault(command => command.Id == selected)
                    ?? (commands.Count > 0 ? commands[0] : null);
                browser.CommandCount.Text = commands.Count + " captured commands";
                browser.CommandEmptyHint.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if ((browser.CommandsList.SelectedItem as SimCommand)?.Id != selected) _selectedPhysicalInput = null;
            }
            finally { _refreshing = false; }
            RefreshRecords();
        }
        private void RefreshRecords()
        {
            var selected = (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.RecordId;
            var command = _assignments.Browser.CommandsList.SelectedItem as SimCommand;
            var rows = _rows.Where(row => _assignments.Browser.CommandsList.Items.Count > 0 && (command is null || row.Command.Id == command.Id)
                && (command is not null || SelectedDevice is null || row.DeviceIds.Contains(SelectedDevice) || row.Binding.Empty)).ToArray();
            _refreshing = true; _assignments.Records.ItemsSource = rows;
            _assignments.Records.SelectedItem = rows.FirstOrDefault(row => row.RecordId == selected) ?? rows.FirstOrDefault();
            _refreshing = false; RefreshEditor();
        }
        private void RefreshEditor()
        {
            _assignments.Editor.MappedInputs = MappedEditorInputs;
            var row = _assignments.Records.SelectedItem as WarThunderWorkspaceRow;
            var command = (_assignments.Browser.CommandsList.SelectedItem as SimCommand) ?? row?.Command;
            if (_selection.CommandId != command?.Id || _selection.AlternativeId != row?.RecordId) _selectedPhysicalInput = null;
            _selection.SelectCommand(command?.Id); _selection.SelectAlternative(row?.RecordId);
            var blocked = _selection.InputBlockedReason;
            _assignments.Editor.Show(_session, command, row, _input.Running, _selection);
            if (blocked.Length != 0) _assignments.Editor.BlockPhysicalSelection(blocked);
            else if (_selectedPhysicalInput is not null && SelectedDevice is { } id) _assignments.Editor.SelectPhysical(id, _selectedPhysicalInput);
        }
        private IReadOnlyList<WarThunderPhysicalInput> MappedEditorInputs(string deviceId)
        {
            if (_session is null) return [];
            var snapshot = _session.Snapshot;
            if (!snapshot.Hardware.TryGetValue(deviceId, out var pin)) return [];
            var observations = _controllerInputs.Where(item => item.Device.PhysicalGuid == pin.Device.PhysicalGuid
                && item.Device.ProductGuid == pin.Device.ProductGuid).Take(2).ToArray();
            if (observations.Length != 1) return [];
            var inputs = WarThunderPhysicalMap.Create(snapshot.Profile, deviceId, observations[0]).Inputs;
            var drawingId = _session.DrawingId(deviceId);
            var diagram = _drawings.FirstOrDefault(item => item.DeviceId == drawingId)?.Diagram;
            if (diagram is null) return inputs;
            return inputs.Select(input =>
            {
                var control = diagram.Controls.FirstOrDefault(item => item.InputKey == input.Key
                    && item.IsAxis == (input.Type == CommandType.Axis));
                return control is null || string.IsNullOrWhiteSpace(control.Label)
                    ? input
                    : input with { Key = control.Label };
            }).ToArray();
        }
        private void RefreshInputState()
        {
            var pin = SelectedDevice is { } id ? _session?.Snapshot.Hardware.GetValueOrDefault(id) : null;
            AssignmentActionPresentation.Show(_assignments.Start, AssignmentAction(WorkspaceAssignmentAction.StartInput,
                pin is not null && GameInputReadiness.Ready(pin, _observed), "Select one linked, connected controller for input testing."));
            AssignmentActionPresentation.Show(_assignments.Stop, AssignmentAction(WorkspaceAssignmentAction.StopInput, true));
            _assignments.Fit.IsEnabled = _presenter.Regions.Count > 0;
            RefreshChrome(); RefreshEditor();
        }
        #endregion END private helpers
    }
}
