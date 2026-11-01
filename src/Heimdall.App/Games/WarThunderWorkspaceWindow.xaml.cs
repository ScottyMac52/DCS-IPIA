using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;
using Microsoft.Win32;

namespace Heimdall.App.Games
{
    /// <summary>Coordinates composed views, shared draft persistence and isolated review export.</summary>
    public partial class WarThunderWorkspaceWindow : Window, IDisposable
    {
        private readonly GameWorkspaceServices _services;
        private readonly WarThunderProfileBackend _backend = new();
        private readonly WarThunderDevicesView _devices = new();
        private readonly WarThunderAssignmentsView _assignments = new();
        private readonly WarThunderReferenceView _references = new();
        private readonly GameWorkspaceFeatureHost _host;
        private readonly HardwareDiagramPresenter _presenter;
        private readonly DiagramViewport _viewport;
        private readonly HardwareInputFeedback _input;
        private readonly GameCommandDragSource _commandDrag;
        private IReadOnlyList<ControllerObservation> _controllerInputs = [];
        private int _hardwareScan;
        private WarThunderPhysicalMap _physicalMap = new([], "Rescan controller inputs.");
        private WarThunderPhysicalInput? _selectedPhysicalInput;
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private readonly Func<GameControllerChange, bool>? _review;
        private readonly IReadOnlyList<WorkspaceHardware>? _suppliedDrawings;
        private IReadOnlyList<WorkspaceHardware> _drawings = [];
        private IReadOnlyList<DetectedDcsDevice> _observed = [];
        private IReadOnlyList<WarThunderWorkspaceRow> _rows = [];
        private GameProfileSession? _session;
        private string? _revision;
        private bool _refreshing;
        private bool _allowClose;
        private bool _disposed;
        public WarThunderWorkspaceWindow(GameWorkspaceServices services, Guid? initial = null,
            Func<MessageBoxResult>? pendingChoice = null, Func<GameControllerChange, bool>? reviewChange = null,
            IReadOnlyList<WorkspaceHardware>? drawings = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null)
        {
            EditionAccess.Policy.RequireSimulator("war-thunder");
            _services = services; _pendingChoice = pendingChoice; _review = reviewChange; _suppliedDrawings = drawings;
            InitializeComponent(); AppearanceManager.Attach(this);
            _presenter = new(_assignments.DrawingCanvas);
            _viewport = new(_assignments.DrawingScroll, _assignments.DrawingCanvas, _assignments.Zoom, "war-thunder.diagram");
            _viewport.EnablePanning();
            _input = new(this, _presenter, text => InputStatus.Text = text, text => _assignments.Activity.Text = text, inputFactory);
            _host = new(FeatureContent, FeatureActions, _input.Stop);
            WireDevices(); WireAssignments();
            _commandDrag = new(_assignments.Browser.CommandsList, "heimdall.war-thunder.command",
                () => _session?.AccessMessage.Length == 0 && !_input.Running && _physicalMap.Inputs.Count > 0);
            FeaturePicker.SelectionChanged += (_, _) =>
            {
                if (_refreshing) return;
                _host.Show(FeaturePicker.SelectedIndex switch { 1 => _assignments, 2 => _references, _ => (System.Windows.UIElement)_devices });
                if (FeaturePicker.SelectedIndex == 1) ShowDrawing();
            };
            Import.Click += (_, _) => Run(() => { var picker = new OpenFileDialog { Filter = "War Thunder controls (*.blk)|*.blk" }; if (picker.ShowDialog(this) == true) ImportFile(picker.FileName); });
            Save.Click += (_, _) => SaveCurrent();
            Discard.Click += (_, _) => { _input.Stop(); _session?.Discard(); if (_session is not null) ProfileName.Text = _session.Snapshot.Name; Refresh(); };
            Export.Click += (_, _) => Run(() => { var picker = new OpenFolderDialog { Title = "Choose a folder for a separate War Thunder review copy" }; if (picker.ShowDialog(this) == true) Status.Text = "Review copy: " + ExportReview(picker.FolderName); });
            Duplicate.Click += (_, _) => Run(DuplicateCurrent);
            ProfileName.TextChanged += (_, _) => { if (!_refreshing) RefreshChrome(); };
            Profiles.SelectionChanged += (_, _) =>
            {
                if (_refreshing) return;
                var selected = Profiles.SelectedItem as GameWorkspaceEntry;
                if (selected is not null) Run(() => { if (ConfirmLeave()) LoadWorkspace(selected.Id); else RefreshEntries(); });
            };
            GamePicker.SelectedIndex = 3;
            GamePicker.SelectionChanged += (_, _) =>
            {
                if (_refreshing || GamePicker.SelectedIndex == 3) return;
                if (ConfirmLeave()) { RequestedGame = (GamePicker.SelectedItem as ComboBoxItem)?.Tag as string; _allowClose = true; Close(); }
                else { _refreshing = true; GamePicker.SelectedIndex = 3; _refreshing = false; }
            };
            Closing += ConfirmClose;
            Closed += (_, _) => Dispose();
            Loaded += async (_, _) =>
            {
                try
                {
                    _drawings = _suppliedDrawings ?? await GameWorkspaceServices.HardwareAsync();
                    if (_disposed) return;
                    await RefreshHardwareAsync();
                    if (_disposed) return;
                    if (_session is null && initial is { } id) LoadWorkspace(id); else { RefreshEntries(); Refresh(); }
                    FeaturePicker.SelectedIndex = 0;
                    _host.Show(_devices);
                }
                catch (Exception error) { ReportError(error); }
            };
            _input.StateChanged += (_, _) => { RefreshDevices(); RefreshInputState(); };
            RefreshChrome();
        }
        #region BEGIN Public
        /// <summary>Gets an explicitly requested cross-game navigation target.</summary>
        public string? RequestedGame { get; private set; }
        internal GameProfileSession? Session => _session;
        internal WarThunderAssignmentsView AssignmentView => _assignments;
        internal WarThunderDevicesView DeviceView => _devices;
        internal void SampleInput() => _input.Sample();
        internal void ImportFile(string path)
        {
            _input.Stop();
            var full = Path.GetFullPath(path);
            if (new FileInfo(full).Length > 8 * 1024 * 1024) throw new InvalidDataException("Controls capture exceeds 8 MiB.");
            var profile = _backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(full)] = File.ReadAllText(full) });
            if (!ConfirmLeave()) return;
            Open(new(1, Guid.NewGuid(), Path.GetFileNameWithoutExtension(full), profile, new()) { SourceDirectory = Path.GetDirectoryName(full) }, null);
            Status.Text = "Imported captured commands. Save this profile, then export a separate review copy.";
        }
        internal void LoadWorkspace(Guid id)
        {
            var saved = _services.Store.Load(id);
            Open(saved.Workspace, saved.Revision);
        }
        internal bool SaveCurrent()
        {
            if (_session is null) return false;
            try
            {
                _input.Stop(); _session.Rename(ProfileName.Text.Trim());
                _revision = _services.Store.Save(_session.Snapshot, _revision); _session.AcceptSaved();
                Remember(); RefreshEntries(); RefreshChrome(); Status.Text = "War Thunder profile saved."; return true;
            }
            catch (Exception error) { ReportError(error, "Save failed"); return false; }
        }
        internal string ExportReview(string parent)
        {
            if (_session is null || Pending) throw new InvalidOperationException("Save the profile before exporting.");
            _input.Stop();
            var plan = _session.Export();
            return GameReviewExport.Write(parent, plan.Files,
                _session.Snapshot.SourceDirectory is { } source ? [source] : [], "war-thunder");
        }
        /// <summary>Stops acquisition and releases feature subscriptions.</summary>
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _hardwareScan++; _commandDrag.Dispose(); _input.Dispose(); _host.Clear();
            if (_session is not null) _session.Changed -= SessionChanged;
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private bool Pending => GameWorkspaceChromeState.For(_session, _revision, ProfileName.Text.Trim()).Pending;
        private string? SelectedDevice => (Controllers.SelectedItem as ListBoxItem)?.Tag as string;
        private void Open(GameWorkspace workspace, string? revision)
        {
            _input.Stop(); _selectedPhysicalInput = null;
            var session = new GameProfileSession(_backend, workspace, EditionAccess.Policy, _drawings.Select(item => item.DeviceId).ToArray(),
                () => _observed, new WarThunderDeviceReferenceMatcher());
            if (_session is not null) _session.Changed -= SessionChanged;
            _session = session; _revision = revision; _session.Changed += SessionChanged;
            _refreshing = true; ProfileName.Text = workspace.Name; _refreshing = false;
            RefreshEntries(); Refresh(); Remember();
        }
        private void SessionChanged(object? sender, EventArgs e) => Refresh();
        private void RefreshEntries()
        {
            _refreshing = true;
            Profiles.ItemsSource = _services.Store.List().Where(item => item.BackendId == "war-thunder").ToArray();
            Profiles.SelectedItem = Profiles.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(item => item.Id == _session?.Snapshot.Id);
            _refreshing = false;
        }
        private void Refresh()
        {
            _refreshing = true;
            var previous = SelectedDevice;
            Controllers.Items.Clear(); Controllers.Items.Add(new ListBoxItem { Content = "All devices and native inputs" });
            if (_session is not null)
                foreach (var device in WarThunderProfileBackend.ReadDevices(_session.Snapshot.Profile))
                    Controllers.Items.Add(new ListBoxItem { Content = device.Name + " · " + device.Id, Tag = device.Id });
            Controllers.SelectedItem = Controllers.Items.Cast<ListBoxItem>().FirstOrDefault(item => item.Tag as string == previous) ?? Controllers.Items[0];
            _rows = _session is null ? [] : WarThunderWorkspaceProjection.Rows(_session.Snapshot.Profile, _session.Catalog.Commands);
            _references.Show(_rows);
            _refreshing = false; RefreshDevices(); RefreshCommands(); RefreshChrome(); ShowDrawing();
        }
        private void RefreshChrome()
        {
            var editable = _session is not null && _session.AccessMessage.Length == 0 && !_input.Running;
            var chrome = GameWorkspaceChromeState.For(_session, _revision, ProfileName.Text.Trim(), _input.Running);
            Save.IsEnabled = editable && chrome.CanSave; Discard.IsEnabled = editable && chrome.CanDiscard;
            Export.IsEnabled = editable && chrome.CanExport; Duplicate.IsEnabled = editable; ProfileName.IsEnabled = editable;
            SavedState.Text = chrome.Status;
        }
        private void DuplicateCurrent()
        {
            if (_session is null || !ConfirmLeave()) return;
            var dialog = new GameConfigurationNameWindow(this, "Duplicate", _session.Snapshot.Name + " copy");
            if (dialog.ShowDialog() != true) return;
            var copy = GameConfigurations.Create(_session.Snapshot, dialog.ConfigurationName, false);
            Open(copy, null);
        }
        private bool ConfirmLeave() => GameWorkspaceLeaveGuard.Confirm(Pending,
            () => (_pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save changes to this War Thunder profile?", "Unsaved changes", MessageBoxButton.YesNoCancel)) switch
            { MessageBoxResult.Yes => GameWorkspaceLeaveDecision.Save, MessageBoxResult.No => GameWorkspaceLeaveDecision.Discard, _ => GameWorkspaceLeaveDecision.Cancel },
            SaveCurrent, () => { _session?.Discard(); if (_session is not null) ProfileName.Text = _session.Snapshot.Name; });
        private void ConfirmClose(object? sender, CancelEventArgs e) { if (!_allowClose && !ConfirmLeave()) e.Cancel = true; }
        private void Remember()
        {
            if (_session is not null && _revision is not null)
                _services.Remember(_services.Recent() with { BackendId = "war-thunder", WorkspaceId = _session.Snapshot.Id, DisplayName = _session.Snapshot.Name });
        }
        private void ReportError(Exception error, string operation = "Operation failed")
        {
            Status.Text = operation + ": " + error.Message;
            ErrorDetails.Text = operation + Environment.NewLine + error;
            ErrorPanel.Visibility = Visibility.Visible;
        }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception error) { ReportError(error); }
        }
        #endregion END private helpers
    }
}
