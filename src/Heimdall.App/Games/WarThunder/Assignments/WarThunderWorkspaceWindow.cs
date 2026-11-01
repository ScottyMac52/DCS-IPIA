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
            browser.CommandsList.SelectionChanged += (_, _) => { if (!_refreshing) { _selectedPhysicalInput = null; RefreshRecords(); } };
            _assignments.Records.SelectionChanged += (_, _) => { if (!_refreshing) RefreshEditor(); };
            _assignments.Editor.PhysicalApplyRequested += (_, _) => Run(() =>
            {
                var command = (_assignments.Browser.CommandsList.SelectedItem as SimCommand) ?? (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.Command;
                if (command is not null && _selectedPhysicalInput is not null) ApplyPhysicalAssignment(command.Id, _selectedPhysicalInput);
            });
            _assignments.Editor.Edited += (_, _) => { Refresh(); Status.Text = "Native edit staged. Save and export to test in War Thunder."; };
            _assignments.Start.Click += (_, _) => Run(() =>
            {
                var pin = SelectedDevice is { } id ? _session?.Snapshot.Hardware.GetValueOrDefault(id) : null;
                if (pin is not null) _input.Start(pin, _observed);
            });
            _assignments.Stop.Click += (_, _) => _input.Stop();
            _assignments.Fit.Click += (_, _) => _viewport.Fit();
            var menu = new ContextMenu();
            var clear = new MenuItem { Header = "Clear joystick alternative" };
            clear.Click += (_, _) => _assignments.Editor.Submit(WarThunderEditKind.Clear);
            var invert = new MenuItem { Header = "Toggle inverse axis" };
            invert.Click += (_, _) => { _assignments.Editor.Inverse.IsChecked = _assignments.Editor.Inverse.IsChecked != true; _assignments.Editor.Submit(WarThunderEditKind.Inverse); };
            menu.Items.Add(clear); menu.Items.Add(invert); _assignments.Records.ContextMenu = menu;
            _assignments.Records.PreviewMouseRightButtonDown += (_, args) =>
            {
                var row = args.OriginalSource is DependencyObject origin ? ItemsControl.ContainerFromElement(_assignments.Records, origin) as DataGridRow : null;
                if (row is not null) row.IsSelected = true; else _assignments.Records.SelectedItem = null;
            };
            _assignments.Records.ContextMenuOpening += (_, args) =>
            {
                clear.Visibility = _assignments.Editor.Clear.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
                invert.Visibility = _assignments.Editor.Inverse.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (clear.Visibility != Visibility.Visible && invert.Visibility != Visibility.Visible) args.Handled = true;
            };
        }
        private void RefreshCommands()
        {
            var browser = _assignments.Browser;
            var selected = (browser.CommandsList.SelectedItem as SimCommand)?.Id;
            var catalog = _session?.Catalog.Commands ?? [];
            var owned = _rows.Where(row => SelectedDevice is null || row.DeviceIds.Contains(SelectedDevice)).ToArray();
            _assignments.AssignmentCount.Text = owned.Count(row => row.DeviceIds.Count > 0) + " captured joystick assignments · " + _rows.Count + " native records";
            var commands = WarThunderWorkspaceProjection.Filter(catalog, _rows, SelectedDevice, browser.CommandSearch.Text,
                browser.CommandCategoryPicker.SelectedIndex > 0 ? browser.CommandCategoryPicker.SelectedItem as string : null,
                (browser.CommandTypePicker.SelectedItem as GameCommandTypeOption)?.Type,
                (browser.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Status ?? GameCommandAssignmentStatus.All);
            _refreshing = true;
            browser.CommandsList.ItemsSource = commands;
            browser.CommandsList.SelectedItem = commands.FirstOrDefault(command => command.Id == selected) ?? commands[0];
            browser.CommandCount.Text = commands.Count + " captured commands";
            browser.CommandEmptyHint.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if ((browser.CommandsList.SelectedItem as SimCommand)?.Id != selected) _selectedPhysicalInput = null;
            _refreshing = false; RefreshRecords();
        }
        private void RefreshRecords()
        {
            var selected = (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.RecordId;
            var command = _assignments.Browser.CommandsList.SelectedItem as SimCommand;
            var rows = _rows.Where(row => (command is null || row.Command.Id == command.Id)
                && (command is not null || SelectedDevice is null || row.DeviceIds.Contains(SelectedDevice) || row.Binding.Empty)).ToArray();
            _refreshing = true; _assignments.Records.ItemsSource = rows;
            _assignments.Records.SelectedItem = rows.FirstOrDefault(row => row.RecordId == selected) ?? rows.FirstOrDefault();
            _refreshing = false; RefreshEditor();
        }
        private void RefreshEditor()
        {
            var row = _assignments.Records.SelectedItem as WarThunderWorkspaceRow;
            _assignments.Editor.Show(_session, (_assignments.Browser.CommandsList.SelectedItem as SimCommand) ?? row?.Command, row, _input.Running);
            if (_selectedPhysicalInput is not null && SelectedDevice is { } id) _assignments.Editor.SelectPhysical(id, _selectedPhysicalInput);
        }
        private void RefreshInputState()
        {
            var pin = SelectedDevice is { } id ? _session?.Snapshot.Hardware.GetValueOrDefault(id) : null;
            _assignments.Start.IsEnabled = !_input.Running && pin is not null && GameInputReadiness.Ready(pin, _observed);
            _assignments.Stop.IsEnabled = _input.Running;
            _assignments.Fit.IsEnabled = _presenter.Regions.Count > 0;
            RefreshChrome(); RefreshEditor();
        }
        #endregion END private helpers
    }
}
