using System.Windows;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Rendering;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Owns ED drawing projection, pointed-control interactions and input-activity presentation.</summary>
    internal sealed class EliteAssignmentDrawing(
        EliteWorkspaceContext context,
        Window owner,
        EliteAssignmentsView _assignmentsView,
        EliteDevicesView _devicesView,
        Action StopLiveInput,
        Action<EliteAssignmentRow> SelectRow,
        Action<string> SelectInput,
        Action Apply,
        Action Clear,
        Action<Action> Run,
        Action<string> Status)
    {
        private readonly HardwareDiagramPresenter _diagram = new(_assignmentsView.DiagramCanvas);
        private readonly DiagramViewport _viewport = new(_assignmentsView.DiagramScroll, _assignmentsView.DiagramCanvas, _assignmentsView.DiagramZoom, "elite.diagram");
        private GameProfileSession? _session => context.Session;
        private bool _busy => context.Busy;
        private IReadOnlyList<SimCommand> _commands => context.Commands;
        private IReadOnlyList<EliteAssignmentRow> _rows => context.Rows;
        private GameDeviceChoice? Device => context.Device;
        private SimCommand? Command => context.Command;
        private string Section => context.Section;
        private string Slot => context.Selection.Slot;
        #region BEGIN Public
        internal byte[] RenderDrawing(WorkspaceHardware hardware, KneeboardComposite page, string footer)
        {
            if (hardware.Diagram.Path.StartsWith("elite-system/", StringComparison.Ordinal))
                return CanonicalSvgRenderer.RenderComplete(PreviewArtwork.Prepare(CompositeKneeboardRenderer.BuildDeviceSvg(hardware.Diagram, page),
                    AppearanceManager.ArtworkOpacityFor(owner)), hardware.Diagram.Width, hardware.Diagram.Height);
            return CompositeKneeboardRenderer.Render(hardware.Diagram, page, footer, artworkOpacity: AppearanceManager.ArtworkOpacityFor(owner));
        }

        internal void RenderDevice()
        {
            _devicesView.ControllerPreview.Clear();
            StopLiveInput(); _assignmentsView.DiagramCanvas.Children.Clear(); _diagram.Regions.Clear(); _assignmentsView.DiagramHint.Visibility = Visibility.Visible;
            UpdateInputActivityVisibility();
            if (_session is null || Device is null) return;
            var hardware = context.CurrentDrawing(Device.Id);
            if (hardware is null) { _assignmentsView.DiagramHint.Text = "No drawing selected. Assign commands using native input keys and the assignments table. You can choose an optional drawing on Devices."; return; }
            var rows = _rows.Where(x => x.DeviceId == Device.Id).ToArray();
            var page = EliteWorkspaceProjection.Diagram(_session.Name, hardware.Diagram, rows, Section);
            _devicesView.ControllerPreview.Show(hardware.Diagram, page, AppearanceManager.ArtworkOpacityFor(owner));
            var section = Section;
            var rowsByInput = rows.Where(x => section == "All" || x.Section == section).ToLookup(x => (string?)x.Input, StringComparer.Ordinal);
            _diagram.Show(hardware.Diagram, page, DiagramInteractions(), AppearanceManager.ArtworkOpacityFor(owner), Tooltip);
            string Tooltip(HardwareGraphicControl control)
            {
                var key = EliteInputKeys.FromDiagram(hardware.Diagram, control);
                return control.Label + " · " + (key ?? control.InputKey) + "\n" +
                    string.Join("\n", rowsByInput[key].Select(x => x.CommandName + " · " + x.Slot +
                        (x.Modifiers.Count > 0 ? " · " + string.Join(" + ", x.Modifiers.Select(m => m.Value)) : "")));
            }
            _assignmentsView.DiagramHint.Visibility = Visibility.Collapsed;
            UpdateInputActivityVisibility();
        }

        internal HardwareDiagramInteractions DiagramInteractions() => new(
            Select: SelectControl, Drop: DropCommand,
            CanClear: CanEditControl, Clear: ClearControl,
            CanTune: control => control.IsAxis && CanEditControl(control) && AssignmentForControl(control)?.IsAxis == true,
            Tune: TuneControl, CanDrop: CanDropCommand, ActionState: ControlAction);

        internal void UpdateInputActivityVisibility()
        {
            var fallback = context.Feature == 1 && _assignmentsView.DiagramHint.Visibility == Visibility.Visible;
            _assignmentsView.InputActivity.Visibility = _assignmentsView.InputActivityHint.Visibility = fallback ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.DiagramScroll.Visibility = fallback ? Visibility.Collapsed : Visibility.Visible;
        }

        internal void ShowInputActivity(string text)
        {
            if (_assignmentsView.InputActivity.Text == text) return;
            _assignmentsView.InputActivity.Text = text; _assignmentsView.InputActivity.ScrollToEnd();
        }

        internal void FitDiagramClicked(object sender, RoutedEventArgs e) => _viewport.Fit();
        internal HardwareDiagramPresenter Presenter => _diagram;
        internal Func<EliteAxisTuningWindow, bool?>? AxisTuningReview { get; set; }
        #endregion END Public
        #region BEGIN private helpers
        private EliteAssignmentRow? AssignmentForControl(HardwareGraphicControl control)
        {
            var key = context.DrawingKey(control);
            if (key is null) return null;
            var rows = _rows.Where(x => x.DeviceId == Device?.Id && x.Input == key && (Section == "All" || x.Section == Section));
            return rows.FirstOrDefault(x => x.CommandId == Command?.Id && x.Slot == Slot) ?? rows.FirstOrDefault();
        }

        private bool CanEditControl(HardwareGraphicControl control) =>
            _session?.AccessMessage.Length == 0 && !_busy && !context.Testing &&
            context.Feature == 1 && AssignmentForControl(control) is not null;

        private void ClearControl(HardwareGraphicControl control) => Run(() =>
        {
            if (!CanEditControl(control) || AssignmentForControl(control) is not { } row) return;
            SelectRow(row);
            Clear();
        });

        private void TuneControl(HardwareGraphicControl control) => Run(() =>
        {
            if (!control.IsAxis || !CanEditControl(control) || AssignmentForControl(control) is not { IsAxis: true } row) return;
            SelectRow(row);
            var dialog = new EliteAxisTuningWindow(owner, row.CommandName, row.Invert, row.Deadzone);
            var session = _session!;
            var invert = row.Invert;
            var deadzone = row.Deadzone;
            _assignmentsView.ExecuteNativeAssignment(row.CommandId, row.Input, row.Slot, () =>
            {
                var accepted = AxisTuningReview is { } review ? review(dialog) : dialog.ShowDialog();
                if (accepted != true || !dialog.TryRead(out invert, out deadzone))
                    return new(false, false, "Axis tuning canceled.");
                return new(true, invert != row.Invert || deadzone != row.Deadzone, "");
            }, () => session.AssignAxis(row.DeviceId, row.CommandId, row.Input, invert, deadzone), WorkspaceAssignmentAction.TuneAxis);
        });

        private void SelectControl(HardwareGraphicControl control)
        {
            var key = context.DrawingKey(control);
            if (key is null) { Status("This drawing input has no verified Elite key. Choose an explicit native input key instead."); return; }
            var row = AssignmentForControl(control);
            if (row is not null) SelectRow(row);
            SelectInput(key); _assignmentsView.InputKey.Focus();
        }

        private void DropCommand(HardwareGraphicControl control, string id) => Run(() =>
        {
            var command = _commands.Single(x => x.Id == id);
            if (control.IsAxis != (command.Type == CommandType.Axis)) throw new InvalidOperationException("Choose an axis command for an axis, or a button command for a button.");
            _assignmentsView.CommandsList.SelectedItem = _assignmentsView.CommandsList.Items.Cast<SimCommand>().First(x => x.Id == id);
            SelectInput(context.DrawingKey(control) ?? throw new InvalidOperationException("Unsupported drawing input; choose an explicit native key."));
            Apply();
        });

        private bool CanDropCommand(HardwareGraphicControl control, string commandId) =>
            _commands.Any(command => command.Id == commandId && (command.Type == CommandType.Axis) == control.IsAxis) && context.DrawingKey(control) is not null;
        private WorkspaceAssignmentActionState ControlAction(HardwareGraphicControl control, WorkspaceAssignmentAction action) =>
            _assignmentsView.Editing.ActionState(action, new(true, action switch
            {
                WorkspaceAssignmentAction.Clear => AssignmentForControl(control) is not null,
                WorkspaceAssignmentAction.TuneAxis => control.IsAxis && AssignmentForControl(control)?.IsAxis == true,
                _ => true
            }, "No editable assignment on owner control."));
        #endregion END private helpers
    }
}
