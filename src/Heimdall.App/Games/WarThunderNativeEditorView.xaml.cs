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
        private WarThunderBindingCapabilities? _capabilities;
        public WarThunderNativeEditorView()
        {
            InitializeComponent();
            Apply.Click += (_, _) => Submit(WarThunderEditKind.Rebind);
            Add.Click += (_, _) => Submit(WarThunderEditKind.Add);
            Clear.Click += (_, _) => Submit(WarThunderEditKind.Clear);
            Inverse.Click += (_, _) => Submit(WarThunderEditKind.Inverse);
            SetParameter.Click += (_, _) => Submit(WarThunderEditKind.Parameter);
            Input.SelectionChanged += (_, _) => { if (!_refreshing) SelectInput(); };
            Parameter.SelectionChanged += (_, _) => { if (Parameter.SelectedItem is WarThunderBlkScalar scalar) { Literal.Text = scalar.Value; Literal.ToolTip = "Native " + scalar.Type + " literal"; } };
            Target.SelectionChanged += (_, _) => UpdateActions();
        }
        #region BEGIN Public
        internal event EventHandler? Edited;
        internal void Show(GameProfileSession? session, SimCommand? command, WarThunderWorkspaceRow? row, bool busy)
        {
            _refreshing = true; _session = session; _row = row; _command = command; _busy = busy;
            Frame.ShowCommand(command);
            var previous = (Target.SelectedItem as WarThunderDevice)?.Id;
            Target.ItemsSource = session is null ? [] : WarThunderProfileBackend.ReadDevices(session.Snapshot.Profile);
            Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().FirstOrDefault(device => device.Id == previous);
            Input.ItemsSource = row is null ? [] : WarThunderWorkspaceProjection.InputFields(row.Binding); Input.SelectedIndex = 0;
            var capabilities = _capabilities = row is null || session is null ? null : WarThunderProfileBackend.GetBindingCapabilities(session.Snapshot.Profile, row.RecordId);
            Parameter.ItemsSource = row?.Binding.Fields.Where(scalar => capabilities!.EditableParameterAddresses.Contains(scalar.Address)).ToArray() ?? [];
            Parameter.SelectedIndex = 0;
            Inverse.IsChecked = row is not null && WarThunderWorkspaceProjection.IsInverted(row.Binding);
            Frame.AssignmentDraftStatus.Text = busy ? "Stop input testing before editing." : session is null ? "Import a controls BLK capture to begin."
                : capabilities is null ? "Select a captured native record."
                : !capabilities.RebindNativeInputs && !capabilities.AddNativeInput && !capabilities.ClearJoystickAlternative
                    && !capabilities.SetAxisInverse && capabilities.EditableParameterAddresses.Count == 0
                    ? "This native record is preserved read-only; no supported edit is available for its captured shape."
                    : "Select a saved native mapping and zero-based index, then apply the supported operation.";
            _refreshing = false; SelectInput(); UpdateActions();
        }
        internal void Submit(WarThunderEditKind kind)
        {
            if (_refreshing || _session is null || _busy) return;
            try
            {
                var index = 0;
                if ((kind is WarThunderEditKind.Rebind or WarThunderEditKind.Add)
                    && !int.TryParse(Index.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                    throw new InvalidOperationException("Enter a zero-based native input index.");
                _session.ApplyNativeEdit(new WarThunderNativeEdit(kind, _row?.RecordId ?? "", (kind == WarThunderEditKind.Parameter ? Parameter.SelectedItem as WarThunderBlkScalar : Input.SelectedItem as WarThunderBlkScalar)?.Address ?? "",
                    (Target.SelectedItem as WarThunderDevice)?.Id ?? "", _command?.Id ?? "", _command?.Type ?? CommandType.Button,
                    index, Inverse.IsChecked == true, Literal.Text));
                Edited?.Invoke(this, EventArgs.Empty);
                Frame.AssignmentDraftStatus.Text = "Native edit staged. Save to retain it, then export a review copy.";
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.IO.InvalidDataException or FormatException or OverflowException)
            { Show(_session, _command, _row, _busy); Frame.AssignmentDraftStatus.Text = error.Message; }
        }
        #endregion END Public
        #region BEGIN private helpers
        private void SelectInput()
        {
            if (_session is null || Input.SelectedItem is not WarThunderBlkScalar scalar || _command is null) { UpdateActions(); return; }
            var target = WarThunderWorkspaceProjection.InputTarget(_session.Snapshot.Profile, scalar, _command.Type);
            if (target is not null)
            {
                Target.SelectedItem = Target.Items.Cast<WarThunderDevice>().Single(device => device.Id == target.DeviceId);
                Index.Text = target.Index.ToString(CultureInfo.InvariantCulture);
            }
            UpdateActions();
        }
        private void UpdateActions()
        {
            if (_refreshing) return;
            var ready = _session is not null && _session.AccessMessage.Length == 0 && !_busy;
            var capabilities = _capabilities;
            Apply.IsEnabled = ready && capabilities?.RebindNativeInputs == true && Target.SelectedItem is WarThunderDevice && Input.SelectedItem is WarThunderBlkScalar;
            Add.IsEnabled = ready && capabilities?.AddNativeInput == true && Target.SelectedItem is WarThunderDevice;
            Clear.IsEnabled = ready && capabilities?.ClearJoystickAlternative == true;
            Inverse.IsEnabled = ready && capabilities?.SetAxisInverse == true;
            SetParameter.IsEnabled = ready && capabilities?.EditableParameterAddresses.Count > 0;
            Target.IsEnabled = ready; Input.IsEnabled = ready; Index.IsEnabled = ready; Parameter.IsEnabled = ready && capabilities?.EditableParameterAddresses.Count > 0;
            Literal.IsEnabled = Parameter.IsEnabled;
        }
        #endregion END private helpers
    }
}
