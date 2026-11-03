using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Collects native indices and literals; the adapter owns parsing, range checks and mutations.</summary>
    public partial class WarThunderNativeEditorView : UserControl
    {
        private GameProfileSession? _session;
        private WarThunderWorkspaceRow? _row;
        private SimCommand? _command;
        private bool _refreshing;
        private bool _busy;
        private bool _physicalSelected;
        private WarThunderNativeEdit? _physicalEdit;
        private GameAssignmentSelection _selection = new();
        private WarThunderBindingCapabilities? _capabilities;
        public WarThunderNativeEditorView()
        {
            InitializeComponent();
            Apply.Click += (_, _) => { if (_physicalSelected) PhysicalApplyRequested?.Invoke(this, EventArgs.Empty); else Submit(_capabilities?.RebindNativeInputs == true ? WarThunderEditKind.Rebind : WarThunderEditKind.Add); };
            Add.Click += (_, _) => Submit(WarThunderEditKind.Add);
            Clear.Click += (_, _) => Submit(WarThunderEditKind.Clear);
            Input.SelectionChanged += (_, _) => { if (!_refreshing) SelectInput(); };
            Target.SelectionChanged += (_, _) => { if (!_refreshing) { _physicalSelected = false; _selection.SelectController((Target.SelectedItem as WarThunderDevice)?.Id); _selection.SelectInput(Index.Text); } UpdateActions(); };
            Index.TextChanged += (_, _) => { if (!_refreshing) { _physicalSelected = false; _selection.SelectInput(Index.Text); } UpdateActions(); };
        }
        #region BEGIN Public
        internal event EventHandler<WarThunderNativeEdit>? EditRequested;
        internal event EventHandler? PhysicalApplyRequested;
        internal void SelectPhysical(string deviceId, WarThunderPhysicalInput input)
        {
            _physicalEdit = null;
            if (_session is not null && _command?.Type == input.Type)
            {
                try { _physicalEdit = WarThunderDrawingProjection.Assignment(_session.Snapshot.Profile, _command.Id, deviceId, input); }
                catch (InvalidOperationException) { }
            }
            _refreshing = true;
            Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().SingleOrDefault(device => device.Id == deviceId);
            Index.Text = input.Index.ToString(CultureInfo.InvariantCulture);
            _refreshing = false; _physicalSelected = true;
            _selection.SelectController(deviceId); _selection.SelectAlternative(_row?.RecordId);
            if (_physicalEdit is null) _selection.BlockInput("This physical input has no verified native mapping.");
            else _selection.SelectInput(input.Key);
            UpdateActions();
        }
        internal void BlockPhysicalSelection(string message)
        {
            _refreshing = true;
            Target.SelectedItem = null; Index.Clear();
            _refreshing = false; _physicalSelected = true; _physicalEdit = null;
            _selection.BlockInput(message);
            Frame.AssignmentDraftStatus.Text = message; UpdateActions();
        }
        internal void Show(GameProfileSession? session, SimCommand? command, WarThunderWorkspaceRow? row, bool busy, GameAssignmentSelection? selection = null)
        {
            if (selection is not null) _selection = selection;
            _refreshing = true; _physicalSelected = false; _physicalEdit = null; _session = session; _row = row; _command = command; _busy = busy;
            Frame.ShowCommand(command);
            var previous = (Target.SelectedItem as WarThunderDevice)?.Id;
            Target.ItemsSource = session is null ? [] : WarThunderProfileBackend.ReadDevices(session.Snapshot.Profile);
            Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().FirstOrDefault(device => device.Id == previous);
            Input.ItemsSource = row is null ? [] : WarThunderWorkspaceProjection.InputFields(row.Binding); Input.SelectedIndex = 0;
            var capabilities = _capabilities = row is null || session is null ? null : WarThunderProfileBackend.GetBindingCapabilities(session.Snapshot.Profile, row.RecordId);
            Frame.AssignmentDraftStatus.Text = busy ? "Stop input testing before editing." : session is null ? "Import a controls BLK capture to begin."
                : capabilities is null ? "Select a captured native record."
                : !capabilities.RebindNativeInputs && !capabilities.AddNativeInput && !capabilities.ClearAssignment
                    && !capabilities.SetAxisInverse && capabilities.EditableParameterAddresses.Count == 0
                    ? "This native record is preserved read-only; no supported edit is available for its captured shape."
                    : "Select a saved native mapping and zero-based index, then apply the supported operation.";
            _refreshing = false; SelectInput(); UpdateActions();
        }
        internal WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action)
        {
            var context = new WorkspaceAssignmentContext(_selection.Scope, _session is not null, _session?.AccessMessage.Length > 0, false, _busy);
            var review = action switch
            {
                WorkspaceAssignmentAction.Apply => ReviewInput(),
                WorkspaceAssignmentAction.AddAlternative => ReviewEdit(WarThunderEditKind.Add),
                _ => new GameAssignmentReview(true, true, "")
            };
            var targetCurrent = !_physicalSelected || (_physicalEdit is not null && _selection.InputBlockedReason.Length == 0);
            var ready = action switch
            {
                WorkspaceAssignmentAction.Apply => review.Valid && review.Changed,
                WorkspaceAssignmentAction.AddAlternative => _capabilities?.AddNativeInput == true && review.Valid && review.Changed,
                WorkspaceAssignmentAction.Clear => _capabilities?.ClearAssignment == true,
                WorkspaceAssignmentAction.TuneAxis => _capabilities?.SetAxisInverse == true || _capabilities?.EditableParameterAddresses.Count > 0,
                _ => false
            };
            return WorkspaceAssignmentActionState.For(action, context, new(true, targetCurrent && ready,
                !targetCurrent ? _selection.InputBlockedReason : action is WorkspaceAssignmentAction.Apply or WorkspaceAssignmentAction.AddAlternative
                    ? review.Valid ? "Assignment unchanged." : review.Message : "Select an editable native assignment that supports this operation."));
        }
        internal void Submit(WarThunderEditKind kind)
        {
            if (_refreshing || _session is null || _busy || (_physicalSelected && (_physicalEdit is null || _selection.InputBlockedReason.Length > 0))) return;
            try
            {
                var edit = CreateEdit(kind);
                if (EditRequested is null) throw new InvalidOperationException("The assignment editor is not connected to its workspace.");
                EditRequested.Invoke(this, edit);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.IO.InvalidDataException or FormatException or OverflowException)
            { Show(_session, _command, _row, _busy); Frame.AssignmentDraftStatus.Text = error.Message; }
        }
        #endregion END Public
        #region BEGIN private helpers
        private WarThunderNativeEdit CreateEdit(WarThunderEditKind kind)
        {
            var index = 0;
            if ((kind is WarThunderEditKind.Rebind or WarThunderEditKind.Add)
                && !int.TryParse(Index.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                throw new InvalidOperationException("Enter a zero-based native input index.");
            return new(kind, _row?.RecordId ?? "", (Input.SelectedItem as WarThunderBlkScalar)?.Address ?? "",
                (Target.SelectedItem as WarThunderDevice)?.Id ?? "", _command?.Id ?? "", _command?.Type ?? CommandType.Button,
        }
        private GameAssignmentReview ReviewInput()
        {
            if (_session is null) return new(false, false, "Import or open a profile first.");
            if (_physicalSelected)
                return _physicalEdit is not null && _selection.InputBlockedReason.Length == 0
                    ? _session.ReviewNativeEdit(_physicalEdit) : new(false, false, _selection.InputBlockedReason);
            return ReviewEdit(_capabilities?.RebindNativeInputs == true ? WarThunderEditKind.Rebind : WarThunderEditKind.Add);
        }
        private GameAssignmentReview ReviewEdit(WarThunderEditKind kind)
        {
            if (_session is null) return new(false, false, "Import or open a profile first.");
            try { return _session.ReviewNativeEdit(CreateEdit(kind)); }
            catch (InvalidOperationException error) { return new(false, false, error.Message); }
        }
        private void SelectInput()
        {
            if (_session is null || Input.SelectedItem is not WarThunderBlkScalar scalar || _command is null) { UpdateActions(); return; }
            var target = WarThunderWorkspaceProjection.InputTarget(_session.Snapshot.Profile, scalar, _command.Type);
            if (target is not null)
            {
                _refreshing = true;
                Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().Single(device => device.Id == target.DeviceId);
                Index.Text = target.Index.ToString(CultureInfo.InvariantCulture);
                _refreshing = false;
                _selection.SelectController(target.DeviceId); _selection.SelectAlternative(_row?.RecordId); _selection.SelectInput(scalar.Address);
            }
            UpdateActions();
        }
        private void UpdateActions()
        {
            if (_refreshing) return;
            AssignmentActionPresentation.Show(Apply, ActionState(WorkspaceAssignmentAction.Apply));
            AssignmentActionPresentation.Show(Add, ActionState(WorkspaceAssignmentAction.AddAlternative));
            AssignmentActionPresentation.Show(Clear, ActionState(WorkspaceAssignmentAction.Clear));
            AssignmentActionPresentation.Show(Tune, ActionState(WorkspaceAssignmentAction.TuneAxis));
            var editable = _session is not null && _session.AccessMessage.Length == 0 && !_busy;
            Target.IsEnabled = editable; Input.IsEnabled = editable; Index.IsEnabled = editable;
        }
        #endregion END private helpers
    }
}
