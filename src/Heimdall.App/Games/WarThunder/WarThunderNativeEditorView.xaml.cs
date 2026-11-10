using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Presents native controller input choices; the adapter owns parsing, range checks and mutations.</summary>
    public partial class WarThunderNativeEditorView : UserControl
    {
        internal Func<WorkspaceAssignmentContext>? CaptureContext { get; set; }
        internal Func<string, IReadOnlyList<WarThunderPhysicalInput>>? MappedInputs { get; set; }
        private GameProfileSession? _session;
        private readonly Dictionary<WarThunderNativeEdit, GameAssignmentReview> _reviews = [];
        private GameProfileSession? _reviewSession;
        private long _reviewStamp;
        private ProfileContentCache<int, IReadOnlyList<WarThunderDevice>>? _devices;
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
            Input.SelectionChanged += (_, _) => { if (!_refreshing) { NativeChoiceChanged(); SelectInput(); } };
            Target.SelectionChanged += (_, _) => { if (!_refreshing) { NativeChoiceChanged(); RefreshInputChoices(); _selection.SelectInput(null); } UpdateActions(); };
            Index.SelectionChanged += (_, _) => { if (!_refreshing) { NativeChoiceChanged(); _selection.SelectInput((Index.SelectedValue as int?)?.ToString(CultureInfo.InvariantCulture)); } UpdateActions(); };
        }
        #region BEGIN Public
        /// <summary>Gets how many distinct native drafts were reviewed, independently of repeated action-state queries.</summary>
        internal int NativeReviewComputations { get; private set; }
        internal event EventHandler<WarThunderNativeEdit>? EditRequested;
        internal event EventHandler? PhysicalApplyRequested;
        internal event EventHandler? NativeSelectionChanged;
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
            RefreshInputChoices(input.Index);
            _refreshing = false; _physicalSelected = true;
            _selection.Update(() =>
            {
                _selection.SelectAlternative(_row?.RecordId);
                if (_physicalEdit is null) _selection.BlockInput("This physical input has no verified native mapping.");
                else _selection.SelectInput(input.Key);
            });
            UpdateActions();
        }
        internal void BlockPhysicalSelection(string message)
        {
            _refreshing = true;
            Target.SelectedItem = null; Index.ItemsSource = null;
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
            Target.ItemsSource = (_devices ??= new(() => _session, (document, _) =>
                Array.AsReadOnly(WarThunderProfileBackend.ReadDevices(document.Profile).ToArray()))).Get(0, []);
            Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().FirstOrDefault(device => device.Id == previous);
            Input.ItemsSource = row is null ? [] : WarThunderWorkspaceProjection.InputFields(row.Binding);
            Input.SelectedIndex = 0;
            NativeDetails.Visibility = Input.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            var capabilities = _capabilities = row is null || session is null ? null : WarThunderProfileBackend.GetBindingCapabilities(session.Snapshot.Profile, row.RecordId);
            Frame.AssignmentDraftStatus.Text = busy ? "Stop input testing before editing." : session is null ? "Import a controls BLK capture to begin."
                : capabilities is null ? "Select a captured native record."
                : !capabilities.RebindNativeInputs && !capabilities.AddNativeInput && !capabilities.ClearAssignment
                    && !capabilities.SetAxisInverse && capabilities.EditableParameterAddresses.Count == 0
                    ? "This native record is preserved read-only; no supported edit is available for its captured shape."
                    : "Select a controller and input, then apply the assignment.";
            RefreshInputChoices();
            _refreshing = false; SelectInput(); UpdateActions();
        }
        internal WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action)
        {
            var context = CaptureContext?.Invoke() ?? new WorkspaceAssignmentContext(_selection.Scope, _session is not null, _session?.AccessMessage.Length > 0, false, _busy);
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
        /// <summary>Updates gates without reloading the pending native field, target or index.</summary>
        internal void RefreshAvailability(bool busy) { _busy = busy; UpdateActions(); }
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
        private void NativeChoiceChanged()
        {
            _physicalSelected = false; _physicalEdit = null;
            NativeSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        private WarThunderNativeEdit CreateEdit(WarThunderEditKind kind)
        {
            var index = Index.SelectedValue as int?;
            if ((kind is WarThunderEditKind.Rebind or WarThunderEditKind.Add)
                && index is null)
                throw new InvalidOperationException("Select a controller input.");
            return new(kind, _row?.RecordId ?? "", (Input.SelectedItem as WarThunderBlkScalar)?.Address ?? "",
                (Target.SelectedItem as WarThunderDevice)?.Id ?? "", _command?.Id ?? "", _command?.Type ?? CommandType.Button,
                index ?? 0);
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
            try
            {
                var edit = CreateEdit(kind);
                var access = _session.AccessMessage;
                if (access.Length != 0) return new(false, false, access);
                if (!ReferenceEquals(_reviewSession, _session) || _reviewStamp != _session.ContentStamp)
                {
                    _reviews.Clear(); _reviewSession = _session; _reviewStamp = _session.ContentStamp;
                }
                if (_reviews.TryGetValue(edit, out var review)) return review;
                if (_reviews.Count >= 32) _reviews.Clear();
                NativeReviewComputations++;
                return _reviews[edit] = _session.ReviewNativeEdit(edit);
            }
            catch (InvalidOperationException error) { return new(false, false, error.Message); }
        }
        private void RefreshInputChoices(int? selected = null)
        {
            var refreshing = _refreshing;
            _refreshing = true;
            try
            {
                if (Target.SelectedItem is not WarThunderDevice { Valid: true } device || _command is null)
                {
                    Index.ItemsSource = null;
                    return;
                }

                var axis = _command.Type == CommandType.Axis;
                var count = axis ? device.AxesCount : device.ButtonsCount;
                var names = (MappedInputs?.Invoke(device.Id) ?? [])
                    .Where(input => input.Type == _command.Type && input.Index >= 0 && input.Index < count)
                    .ToLookup(input => input.Index, input => input.Key);
                // Imported ranges are untrusted. Large ranges can still use verified and captured inputs.
                IEnumerable<int> indices = count <= 4096 ? Enumerable.Range(0, count) : names.Select(group => group.Key);
                if (selected is int current && current >= 0 && current < count)
                    indices = indices.Append(current);
                Index.ItemsSource = indices.Distinct().Order().Select(index =>
                {
                    var label = names.Contains(index) ? string.Join(" / ", names[index].Distinct(StringComparer.Ordinal))
                        : string.Create(CultureInfo.InvariantCulture, $"Native {(axis ? "axis slot" : "button slot")} {index}");
                    return new KeyValuePair<int, string>(index, label);
                }).ToArray();
                Index.SelectedValue = selected;
            }
            finally { _refreshing = refreshing; }
        }

        private void SelectInput()
        {
            if (_session is null || Input.SelectedItem is not WarThunderBlkScalar scalar || _command is null) { UpdateActions(); return; }
            var target = WarThunderWorkspaceProjection.InputTarget(_session.Snapshot.Profile, scalar, _command.Type);
            if (target is not null)
            {
                _refreshing = true;
                Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().Single(device => device.Id == target.DeviceId);
                RefreshInputChoices(target.Index);
                _refreshing = false;
                _selection.Update(() => { _selection.SelectAlternative(_row?.RecordId); _selection.SelectInput(scalar.Address); });
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
            Target.IsEnabled = editable; Input.IsEnabled = editable; Index.IsEnabled = editable && Index.HasItems;
        }
        #endregion END private helpers
    }
}
