using System.Text.Json;
using System.Windows;
using Heimdall.Application;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Coordinates physical callout selection, native context actions and truthful staged-edit feedback.</summary>
    public partial class WarThunderWorkspaceWindow
    {
        private readonly Dictionary<string, IReadOnlyList<WarThunderWorkspaceRow>> _drawingAssignments = new(StringComparer.Ordinal);
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void SelectPhysicalControl(HardwareGraphicControl control)
        {
            var type = (_assignments.Browser.CommandsList.SelectedItem as Heimdall.Domain.MultiSim.SimCommand)?.Type
                ?? (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.Command.Type;
            _selectedPhysicalInput = _physicalMap.Inputs.FirstOrDefault(input => input.Key == control.InputKey && input.Type == type);
            RefreshEditor();
            _assignments.Artwork.Feedback.Text = _selectedPhysicalInput is null ? "This physical input has no verified native mapping. " + _physicalMap.Message : control.Label + " selected. Apply assignment to stage the selected command.";
        }
        internal void DropPhysicalCommand(HardwareGraphicControl control, string commandId) => Run(() =>
        {
            if (_session is null) return;
            _assignments.Browser.CommandsList.SelectedItem = _assignments.Browser.CommandsList.Items.Cast<Heimdall.Domain.MultiSim.SimCommand>().FirstOrDefault(command => command.Id == commandId);
            var type = _session.Catalog.Find(commandId)?.Type;
            var input = _physicalMap.Inputs.FirstOrDefault(candidate => candidate.Key == control.InputKey && candidate.Type == type)
                ?? throw new InvalidOperationException("This physical input has no verified native mapping.");
            ApplyPhysicalAssignment(commandId, input);
        });
        private bool CanDropPhysicalCommand(HardwareGraphicControl control, string commandId)
        {
            if (_session is null || SelectedDevice is not { } id || _input.Running || _session.AccessMessage.Length != 0) return false;
            var type = _session.Catalog.Find(commandId)?.Type;
            var input = _physicalMap.Inputs.FirstOrDefault(candidate => candidate.Key == control.InputKey && candidate.Type == type);
            if (input is null) return false;
            try { _ = WarThunderDrawingProjection.Assignment(_session.Snapshot.Profile, commandId, id, input); return true; }
            catch (InvalidOperationException) { return false; }
        }
        private void ApplyPhysicalAssignment(string commandId, WarThunderPhysicalInput input)
        {
            if (_session is null || SelectedDevice is not { } id || _input.Running) throw new InvalidOperationException("Select a linked controller and stop input testing before assigning.");
            if (!_physicalMap.Inputs.Contains(input)) throw new InvalidOperationException("The physical input mapping is no longer current.");
            _assignments.Browser.CommandsList.SelectedItem = _assignments.Browser.CommandsList.Items.Cast<Heimdall.Domain.MultiSim.SimCommand>().FirstOrDefault(command => command.Id == commandId);
            ApplyDrawingEdit(WarThunderDrawingProjection.Assignment(_session.Snapshot.Profile, commandId, id, input));
        }
        private void ApplyDrawingEdit(WarThunderNativeEdit edit)
        {
            if (_session is null || _input.Running) throw new InvalidOperationException("Stop input testing before editing.");
            var before = JsonSerializer.Serialize(_session.Snapshot.Profile);
            _session.ApplyNativeEdit(edit);
            var changed = before != JsonSerializer.Serialize(_session.Snapshot.Profile);
            _selectedPhysicalInput = null; RefreshEditor(); RefreshChrome();
            Status.Text = _assignments.Artwork.Feedback.Text = changed
                ? "Assignment changed and staged. Save before exporting."
                : "Assignment is already set; no binding changed.";
        }
        private IReadOnlyList<WarThunderWorkspaceRow> PhysicalAssignments(HardwareGraphicControl control)
        {
            if (_session is null || SelectedDevice is not { } id) return [];
            if (!_drawingAssignments.TryGetValue(control.InputKey, out var rows))
            {
                rows = WarThunderDrawingProjection.Assignments(_session.Snapshot.Profile, id, control.InputKey, _physicalMap);
                _drawingAssignments.Add(control.InputKey, rows);
            }
            return rows;
        }
        private IReadOnlyList<WarThunderWorkspaceRow> ClearableAssignments(HardwareGraphicControl control)
        {
            if (_session is null || _input.Running || _session.AccessMessage.Length != 0) return [];
            return PhysicalAssignments(control).Where(row =>
                WarThunderProfileBackend.GetBindingCapabilities(_session.Snapshot.Profile, row.RecordId).ClearJoystickAlternative).ToArray();
        }
        private bool CanClearPhysicalControl(HardwareGraphicControl control) => ClearableAssignments(control).Count > 0;
        private IReadOnlyList<HardwareDiagramClearChoice> ClearPhysicalChoices(HardwareGraphicControl control) =>
            ClearableAssignments(control).Select((row, index) => new HardwareDiagramClearChoice(
                row.Name + " · " + row.Inputs + " · " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => Run(() => ApplyDrawingEdit(new(WarThunderEditKind.Clear, row.RecordId))))).ToArray();
        private void ClearPhysicalControl(HardwareGraphicControl control) => Run(() =>
        {
            var rows = ClearableAssignments(control);
            if (rows.Count != 1) throw new InvalidOperationException("Choose the native alternative in the Clear Assignment submenu.");
            ApplyDrawingEdit(new(WarThunderEditKind.Clear, rows[0].RecordId));
        });
        private string ClearUnavailableReason(HardwareGraphicControl control)
        {
            if (_input.Running) return "Stop input testing before clearing an assignment.";
            if (_session is null) return "Import or open a profile first.";
            if (_session.AccessMessage.Length != 0) return _session.AccessMessage;
            var rows = PhysicalAssignments(control);
            if (rows.Count == 0) return "No captured assignment is mapped to this physical control. " + _physicalMap.Message;
            if (control.IsAxis) return "Native axis clearing is not implemented. Use View assignments to inspect the captured axis settings.";
            return "These native alternatives contain system inputs, nested data or unsupported syntax. View assignments to inspect the exact records.";
        }
        private bool CanTunePhysicalControl(HardwareGraphicControl control) => _session is not null && !_input.Running
            && _session.AccessMessage.Length == 0 && PhysicalAssignments(control).Any(row =>
            {
                var capabilities = WarThunderProfileBackend.GetBindingCapabilities(_session.Snapshot.Profile, row.RecordId);
                return capabilities.SetAxisInverse || capabilities.EditableParameterAddresses.Count > 0;
            });
        private void TunePhysicalControl(HardwareGraphicControl control) => Run(() =>
        {
            InspectPhysicalControl(control);
            var row = _assignments.Records.Items.Cast<WarThunderWorkspaceRow>().FirstOrDefault(candidate =>
            {
                var capabilities = WarThunderProfileBackend.GetBindingCapabilities(_session!.Snapshot.Profile, candidate.RecordId);
                return capabilities.SetAxisInverse || capabilities.EditableParameterAddresses.Count > 0;
            });
            _assignments.Records.SelectedItem = row; RefreshEditor();
            _assignments.Editor.NativeDetails.IsExpanded = true;
            if (_assignments.Editor.Inverse.IsEnabled) _assignments.Editor.Inverse.Focus(); else _assignments.Editor.Parameter.Focus();
        });
        private void InspectPhysicalControl(HardwareGraphicControl control) => Run(() =>
        {
            var rows = PhysicalAssignments(control);
            _selectedPhysicalInput = null;
            _refreshing = true;
            try
            {
                _assignments.Browser.CommandsList.SelectedItem = null;
                _assignments.Records.ItemsSource = rows;
                _assignments.Records.SelectedItem = rows.Count > 0 ? rows[0] : null;
                _assignments.CapturedAssignments.IsExpanded = true;
            }
            finally { _refreshing = false; }
            RefreshEditor();
            _assignments.Artwork.Feedback.Text = rows.Count == 0 ? "No captured assignment on " + control.Label + "." : "Showing assignments on " + control.Label + ".";
        });
        #endregion END private helpers
    }
}
