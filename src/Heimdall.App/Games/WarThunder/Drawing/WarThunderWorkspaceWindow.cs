using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Coordinates physical callout selection, native context actions and truthful staged-edit feedback.</summary>
    public partial class WarThunderWorkspaceWindow
    {
        private readonly Dictionary<string, IReadOnlyList<WarThunderWorkspaceRow>> _drawingAssignments = new(StringComparer.Ordinal);
        #region BEGIN Public
        internal Action<AxisTuningWindow>? AxisTuningOpened { get; set; }
        #endregion END Public
        #region BEGIN private helpers
        private void SelectPhysicalControl(HardwareGraphicControl control)
        {
            var type = (_assignments.Browser.CommandsList.SelectedItem as Heimdall.Domain.MultiSim.SimCommand)?.Type
                ?? (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.Command.Type;
            _selectedPhysicalInput = _physicalMap.Inputs.FirstOrDefault(input => input.Key == control.InputKey && input.Type == type);
            if (_selectedPhysicalInput is null) _selection.BlockInput("This physical input has no verified native mapping. " + _physicalMap.Message);
            else _selection.SelectInput(_selectedPhysicalInput.Key);
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
            var result = ExecuteAssignment(edit);
            _selectedPhysicalInput = null; _selection.SelectInput(null); RefreshEditor(); RefreshChrome();
            Status.Text = _assignments.Artwork.Feedback.Text = _assignments.Editor.Frame.AssignmentDraftStatus.Text = result.Message;
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
        private WarThunderWorkspaceRow[] ClearableAssignments(HardwareGraphicControl control)
        {
            if (_session is null || _input.Running || _session.AccessMessage.Length != 0) return [];
            return PhysicalAssignments(control).Where(row =>
                WarThunderProfileBackend.GetBindingCapabilities(_session.Snapshot.Profile, row.RecordId).ClearAssignment).ToArray();
        }
        private bool CanClearPhysicalControl(HardwareGraphicControl control) => ClearableAssignments(control).Length > 0;
        private HardwareDiagramClearChoice[] ClearPhysicalChoices(HardwareGraphicControl control) =>
            ClearableAssignments(control).Select((row, index) => new HardwareDiagramClearChoice(
                row.Name + " · " + row.Inputs + " · " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => Run(() => ApplyDrawingEdit(new(WarThunderEditKind.Clear, row.RecordId))))).ToArray();
        private void ClearPhysicalControl(HardwareGraphicControl control) => Run(() =>
        {
            var rows = ClearableAssignments(control);
            if (rows.Length != 1) throw new InvalidOperationException("Choose the native alternative in the Clear Assignment submenu.");
            ApplyDrawingEdit(new(WarThunderEditKind.Clear, rows[0].RecordId));
        });
        private string ClearUnavailableReason(HardwareGraphicControl control)
        {
            if (_input.Running) return "Stop input testing before clearing an assignment.";
            if (_session is null) return "Import or open a profile first.";
            if (_session.AccessMessage.Length != 0) return _session.AccessMessage;
            var rows = PhysicalAssignments(control);
            if (rows.Count == 0) return "No captured assignment is mapped to this physical control. " + _physicalMap.Message;
            if (control.IsAxis) return "This axis record is nested, repeated, unsupported or already unassigned. View assignments to inspect it.";
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
            if (!ControlAction(control, WorkspaceAssignmentAction.TuneAxis).Enabled || _session is not { } session) return;
            var profile = session.Snapshot.Profile;
            var row = PhysicalAssignments(control).First(candidate =>
            {
                var support = WarThunderProfileBackend.GetBindingCapabilities(profile, candidate.RecordId);
                return support.SetAxisInverse || support.EditableParameterAddresses.Count > 0;
            });
            TuneNativeAxis(session, row, control.InputKey);
        });

        private void TuneSelectedNativeAxis() => Run(() =>
        {
            if (_session is { } session && _assignments.Records.SelectedItem is WarThunderWorkspaceRow row &&
                _assignments.Editor.ActionState(WorkspaceAssignmentAction.TuneAxis).Enabled)
                TuneNativeAxis(session, row, "");
        });

        private void TuneNativeAxis(GameProfileSession session, WarThunderWorkspaceRow row, string inputKey)
        {
            var capabilities = WarThunderProfileBackend.GetBindingCapabilities(session.Snapshot.Profile, row.RecordId);
            var fields = new StackPanel();
            var inverted = WarThunderWorkspaceProjection.IsInverted(row.Binding);
            var invert = new CheckBox { Content = "Invert axis", IsChecked = inverted, Margin = new Thickness(0, 0, 0, 8) };
            invert.SetResourceReference(FrameworkElement.StyleProperty, "AppCheckBox");
            if (capabilities.SetAxisInverse) fields.Children.Add(invert);
            var editors = new List<(WarThunderBlkScalar Field, TextBox Editor)>();
            foreach (var field in row.Binding.Fields.Where(field => capabilities.EditableParameterAddresses.Contains(field.Address)
                && (!capabilities.SetAxisInverse || WarThunderWorkspaceProjection.FieldName(field) != "inverse")))
            {
                var label = WarThunderWorkspaceProjection.FieldName(field);
                var editor = new TextBox { Text = field.Value, Margin = new Thickness(0, 3, 0, 10) };
                editor.SetResourceReference(FrameworkElement.StyleProperty, "AppTextBox");
                fields.Children.Add(new Label { Content = label, Target = editor });
                fields.Children.Add(editor);
                editors.Add((field, editor));
            }

            var target = new WorkspaceAssignmentTarget(AssignmentContext().Scope, row.Command.Id, inputKey, row.RecordId);
            var host = new WorkspaceAssignmentHost(AssignmentContext, () => JsonSerializer.Serialize(_session?.Snapshot));
            WarThunderAxisTuningEdit? edit = null;
            var review = new GameAssignmentReview(false, false, "Axis tuning canceled.");
            var dialog = new AxisTuningWindow(this, row.Command.DisplayName, fields, () =>
            {
                var changes = editors.Where(item => item.Field.Value != item.Editor.Text)
                    .Select(item => new WarThunderNativeEdit(WarThunderEditKind.Parameter, row.RecordId,
                        item.Field.Address, Literal: item.Editor.Text)).ToList();
                if (capabilities.SetAxisInverse && (invert.IsChecked == true) != inverted)
                    changes.Add(new(WarThunderEditKind.Inverse, row.RecordId, Inverted: invert.IsChecked == true));
                edit = new WarThunderAxisTuningEdit(changes);
                review = session.ReviewNativeEdit(edit);
                return review.Valid ? null : review.Message;
            });
            dialog.Loaded += (_, _) => AxisTuningOpened?.Invoke(dialog);
            var result = AssignmentWorkflow.Execute(host, new WorkspaceAssignmentOperation(target,
                () => dialog.ShowDialog() == true ? review : new(false, false, "Axis tuning canceled."),
                () =>
                {
                    session.ApplyNativeEdit(edit!);
                    return new(DraftOutcome.Applied, "");
                }));
            Status.Text = _assignments.Artwork.Feedback.Text = _assignments.Editor.Frame.AssignmentDraftStatus.Text = result.Message;
        }
        private void InspectPhysicalControl(HardwareGraphicControl control) => Run(() =>
        {
            var rows = PhysicalAssignments(control);
            _selectedPhysicalInput = null; _selection.SelectInput(null);
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
