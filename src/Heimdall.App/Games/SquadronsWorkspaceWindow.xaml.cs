using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Microsoft.Win32;

namespace Heimdall.App.Games
{
    /// <summary>Hosts captured Squadrons profiles with guarded native editing, shared persistence and review-only export.</summary>
    public partial class SquadronsWorkspaceWindow : Window, IDisposable
    {
        private readonly GameWorkspaceServices _services;
        private readonly SquadronsProfileBackend _backend = new();
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private readonly Func<GameControllerChange, bool>? _reviewChange;
        private readonly GameControllerIdentification _identification;
        private readonly GameControllerReordering _reordering;
        private readonly EliteDiagramPresenter _drawingPresenter;
        private readonly DiagramViewport _drawingViewport;
        private Point? _commandDragStart;
        private IReadOnlyList<WorkspaceHardware> _drawings = [];
        private GameProfileSession? _session;
        private string? _revision;
        private string? _sourceDirectory;
        private IReadOnlyList<SquadronsWorkspaceRow> _rows = [];
        private string? _focusedConcept;
        private IReadOnlyList<ControllerObservation> _controllerInputs = [];
        private int _inputScan;
        private bool _scanningInputs;
        private bool _disposed;
        private bool _refreshing = true;
        private bool _allowClose;
        /// <summary>Opens an explicitly chosen Squadrons workspace without importing active settings automatically.</summary>
        public SquadronsWorkspaceWindow(GameWorkspaceServices services, Guid? initial = null, Func<MessageBoxResult>? pendingChoice = null,
            Func<GameControllerChange, bool>? reviewChange = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? identificationFactory = null)
        {
            EditionAccess.Policy.RequireSimulator("star-wars-squadrons");
            _services = services; _pendingChoice = pendingChoice; _reviewChange = reviewChange;
            InitializeComponent(); AppearanceManager.Attach(this);
            _drawingPresenter = new(DrawingCanvas);
            _drawingViewport = new(DrawingScroll, DrawingCanvas, DrawingZoom, "squadrons.diagram");
            _drawingViewport.EnableSmartNavigation();
            // Any drawing-area drop outside a verified callout must be explained.
            DrawingCanvas.AllowDrop = true;
            DrawingCanvas.DragOver += (_, e) =>
            {
                if (!e.Handled && e.Data.GetDataPresent(SquadronsCommandDragFormat))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            };
            DrawingCanvas.Drop += (_, e) =>
            {
                if (!e.Handled && e.Data.GetDataPresent(SquadronsCommandDragFormat))
                {
                    DrawingDropFeedback.Text = "Drop onto an outlined control callout, not the drawing background.";
                    StatusText.Text = DrawingDropFeedback.Text;
                    e.Handled = true;
                }
            };
            _reordering = new(DevicePicker, (source, target, after) =>
            {
                if (source == "all" || target == "all") return;
                Run(() => ReviewNativeChange(_session!.PreviewMoveCapturedSlot(source, target, after)));
            }, id => id.StartsWith("squadrons.joystick.", StringComparison.Ordinal));
            _identification = new(this, ShowIdentification, ClearIdentification, message => StatusText.Text = message, identificationFactory);
            GamePicker.SelectedIndex = 2;
            SectionPicker.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" }; SectionPicker.SelectedIndex = 1;
            TypePicker.ItemsSource = new[] { "All inputs", "Supported physical inputs", "Axes", "Buttons and hats", "Preserved inputs" }; TypePicker.SelectedIndex = 0;
            CommandContextFilter.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" };
            CommandInputFilter.ItemsSource = new[] { "All types", "Axes", "Buttons and hats", "Unknown / unbound" };
            CommandStatusFilter.ItemsSource = new[] { "All", "Assigned to selected controller", "Not assigned to selected controller", "Assigned anywhere", "Unassigned everywhere" };
            CommandContextFilter.SelectedIndex = CommandInputFilter.SelectedIndex = CommandStatusFilter.SelectedIndex = 0;
            Loaded += async (_, _) =>
            {
                await RefreshControllerInputsAsync();
                try
                {
                    _drawings = await GameWorkspaceServices.HardwareAsync();
                    if (!_disposed)
                    {
                        _session?.SetDrawingCatalog(_drawings.Select(x => x.DeviceId).ToArray());
                        Run(RenderDrawing);
                    }
                }
                catch (Exception error) { if (!_disposed) DrawingHint.Text = "Shared drawing catalog unavailable: " + error.Message; }
            };
            Closing += ConfirmClose;
            Closed += (_, _) => Dispose();
            _refreshing = false;
            ReloadChoices();
            if (initial is { } id) Run(() => LoadProfile(id));
            else Run(() => Refresh());
            Run(RescanControllers);
        }
        #region BEGIN Public
        /// <summary>Releases identification handles and foreground lifecycle subscriptions.</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _inputScan++; _identification.Dispose(); _reordering.Dispose(); GC.SuppressFinalize(this); }
        /// <summary>Refreshes capability evidence asynchronously; failed, obsolete or closed-window scans cannot enable assignments.</summary>
        internal async Task RefreshControllerInputsAsync()
        {
            if (_disposed) return;
            var scan = ++_inputScan; _scanningInputs = true; _controllerInputs = [];
            try
            {
                Refresh(false); StatusText.Text = "Inspecting connected controller inputs…";
                var inputs = await _services.DiscoverInputsAsync();
                if (scan != _inputScan || _disposed) return;
                _controllerInputs = inputs;
                StatusText.Text = "Controller inputs inspected. Physical input support does not establish Squadrons slot identity.";
            }
            catch (Exception error) { if (scan == _inputScan && !_disposed) StatusText.Text = "Controller input verification unavailable: " + error.Message; }
            finally { if (scan == _inputScan && !_disposed) { _scanningInputs = false; Run(() => Refresh(false)); } }
        }
        /// <summary>Gets the game explicitly requested after the pending-change guard accepted leaving.</summary>
        public string? RequestedGame { get; private set; }
        /// <summary>Gets the isolated editing boundary for deterministic Windows smoke checks.</summary>
        internal GameProfileSession? Session => _session;
        /// <summary>Samples the shared identification boundary deterministically in Windows smoke tests.</summary>
        internal void SampleIdentification() => _identification.Sample();
        /// <summary>Imports one selected native file with bounded input and preserves the current session on failure.</summary>
        internal void ImportFile(string path)
        {
            if (!ConfirmLeave()) return;
            _identification.Stop();
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Squadrons controls exceed the 4 MiB import limit.");
            var profile = _backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(path) });
            var name = "Squadrons Controls"; var number = 2; var existing = _services.Store.List().Where(x => x.BackendId == _backend.SimId).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (existing.Contains(name)) name = "Squadrons Controls " + number++;
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name, profile, new());
            var opened = CreateSession(workspace);
            _session = opened; _revision = null; _sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            _services.Remember(_services.Recent() with { BackendId = _backend.SimId, WorkspaceId = null, DisplayName = "" });
            ReloadChoices(); Refresh(); StatusText.Text = "Controls imported. Save this profile before exporting.";
        }
        /// <summary>Saves through the shared optimistic revision boundary; failures retain pending edits.</summary>
        internal bool SaveProfile()
        {
            try
            {
                if (_session is null) return false;
                _session.Rename(ProfileName.Text);
                _revision = _services.Store.Save(_session.Snapshot, _revision);
                _session.AcceptSaved(); Remember(); ReloadChoices(); Refresh();
                StatusText.Text = "Squadrons profile saved."; return true;
            }
            catch (Exception error) { StatusText.Text = "Unable to save: " + error.Message; UpdateAvailability(); return false; }
        }
        /// <summary>Writes a new review folder after saving; no active game settings are replaced.</summary>
        internal string ExportReview(string parent)
        {
            if (_session is null || Pending) throw new InvalidOperationException("Save the Squadrons profile before exporting.");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var roots = new List<string> { Path.Combine(documents, "STAR WARS Squadrons", "settings"), Path.Combine(documents, "STAR WARS Squadrons Steam", "settings") };
            if (_sourceDirectory is not null) roots.Add(_sourceDirectory);
            var directory = GameReviewExport.Write(parent, _session.Export().Files, roots, backendId: _backend.SimId);
            StatusText.Text = "Review copy exported to " + directory + ". Close Squadrons and back up its settings before manually installing this copy.";
            return directory;
        }
        #endregion END Public
        #region BEGIN private helpers
        private bool Pending => _session is not null && (_revision is null || _session.HasChanges || ProfileName.Text != _session.Snapshot.Name);
        private GameProfileSession CreateSession(GameWorkspace workspace)
        {
            if (workspace.Profile.BackendId != _backend.SimId) throw new InvalidDataException("This is not a Squadrons profile.");
            return new(_backend, workspace, EditionAccess.Policy, _drawings.Select(x => x.DeviceId).ToArray(), _services.Discover, controllerInputs: () => _controllerInputs);
        }
        private void LoadProfile(Guid id)
        {
            _identification.Stop();
            var saved = _services.Store.Load(id); var opened = CreateSession(saved.Workspace);
            _session = opened; _revision = saved.Revision; _sourceDirectory = null;
            Remember(); ReloadChoices(); Refresh();
        }
        private void Remember()
        {
            if (_session is not null) _services.Remember(_services.Recent() with { BackendId = _backend.SimId, WorkspaceId = _session.Snapshot.Id, DisplayName = _session.Snapshot.Name });
        }
        private void ReloadChoices()
        {
            _refreshing = true;
            try
            {
                ProfilePicker.ItemsSource = _services.Store.List().Where(x => x.BackendId == _backend.SimId).ToArray();
                ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id);
            }
            finally { _refreshing = false; }
        }
        private void Refresh(bool resetName = true)
        {
            var selected = (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
            _refreshing = true;
            var snapshot = _session?.Snapshot;
            if (resetName) ProfileName.Text = snapshot?.Name ?? "";
            var devices = snapshot is null ? [] : SquadronsWorkspaceProjection.OrderedDevices(snapshot.Profile).Select(x => new GameDeviceChoice(x.LogicalId, "Slot " + x.Identity!.SimSpecificIds["squadrons.slot"] + " · " + x.Identity.WindowsName)).ToArray();
            var systemDevices = new[]
            {
                new GameDeviceChoice("system.0", "Mouse · read-only"),
                new GameDeviceChoice("system.2", "Keyboard · read-only"),
                new GameDeviceChoice("system.1", "Gamepad · read-only")
            };
            DevicePicker.ItemsSource = new[] { new GameDeviceChoice("all", "All devices and inputs") }
                .Concat(devices).Concat(snapshot is null ? [] : systemDevices).ToArray();
            DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == selected) ?? DevicePicker.Items[0];
            RefreshCommandCatalog(snapshot);
            if (snapshot is null) _focusedConcept = null;
            TargetDevice.ItemsSource = devices;
            var verified = new Dictionary<string, IReadOnlyCollection<string>>();
            if (snapshot is not null)
                foreach (var (id, pin) in snapshot.Hardware)
                    if (GameControllerInputs.VerifiedKeys(pin, _controllerInputs) is { } keys) verified[id] = keys;
            _rows = snapshot is null ? [] : SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(snapshot.Profile), verified);
            _refreshing = false; RefreshRows(); UpdateAvailability(); UpdateControllerAvailability(); RenderDrawing();
        }
        /// <summary>Refreshes the command catalog independently of the captured-binding table.</summary>
        private void RefreshCommandCatalog(GameWorkspace? snapshot)
        {
            var selected = (CommandPicker.SelectedItem as Heimdall.Domain.MultiSim.SimCommand)?.Id;
            IReadOnlyList<Heimdall.Domain.MultiSim.SimCommand> all = snapshot is null ? [] : _backend.GetCatalog(snapshot.Profile).Commands;
            IReadOnlyList<Heimdall.Domain.MultiSim.SimCommand> filtered = snapshot is null ? [] : SquadronsCommandFilter.Apply(all,
                _backend.ReadBindings(snapshot.Profile),
                CommandContextFilter.SelectedItem as string ?? "All",
                CommandInputFilter.SelectedItem as string ?? "All types",
                CommandStatusFilter.SelectedItem as string ?? "All",
                CommandSearch.Text, SelectedAssignmentSource);
            var wasRefreshing = _refreshing;
            _refreshing = true;
            try
            {
                CommandPicker.ItemsSource = filtered;
                CommandPicker.SelectedItem = filtered.FirstOrDefault(x => x.Id == selected);
            }
            finally { _refreshing = wasRefreshing; }
            CommandFilterCount.Text = filtered.Count + " of " + all.Count +
                " known/imported commands · assignment status includes all native input records.";
            ShowCommandButton.IsEnabled = CommandPicker.SelectedItem is not null;
        }
        private void CommandFiltersChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_refreshing) RefreshCommandCatalog(_session?.Snapshot);
        }
        private void CommandSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (!_refreshing) RefreshCommandCatalog(_session?.Snapshot);
        }
        private void ResetCommandFiltersClicked(object sender, RoutedEventArgs e)
        {
            _refreshing = true;
            try
            {
                CommandContextFilter.SelectedIndex = 0;
                CommandInputFilter.SelectedIndex = 0;
                CommandStatusFilter.SelectedIndex = 0;
                CommandSearch.Clear();
            }
            finally { _refreshing = false; }
            RefreshCommandCatalog(_session?.Snapshot);
        }
        private const string SquadronsCommandDragFormat = "heimdall.squadrons.command";
        /// <summary>Renders only the explicitly pinned shared drawing for a selected joystick slot.</summary>
        private void RenderDrawing()
        {
            DrawingCanvas.Children.Clear();
            _drawingPresenter.Regions.Clear();
            DrawingHint.Visibility = Visibility.Visible;
            var snapshot = _session?.Snapshot;
            var slot = SelectedController;
            var pin = slot is null ? null : snapshot?.Hardware.GetValueOrDefault(slot);
            // A native physical link does not imply an artwork catalog identity.
            var oldRefreshing = _refreshing;
            _refreshing = true;
            try
            {
                DrawingPicker.ItemsSource = _drawings.Where(x => !string.IsNullOrWhiteSpace(x.Diagram.Svg))
                    .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray();
                DrawingPicker.SelectedItem = _drawings.FirstOrDefault(x => x.DeviceId == pin?.HardwareId);
            }
            finally { _refreshing = oldRefreshing; }
            DrawingPicker.IsEnabled = ClearDrawingButton.IsEnabled = pin is not null && _drawings.Count > 0;
            if (DrawingPanel.IsExpanded != true) return;
            if (slot is null || pin is null)
            {
                DrawingHint.Text = "Select a linked joystick controller before choosing its hardware drawing.";
                return;
            }
            if (pin.DrawingHidden)
            {
                DrawingHint.Text = "Drawing hidden. Select a hardware drawing to show it again.";
                return;
            }
            if (string.IsNullOrEmpty(pin.HardwareId))
            {
                DrawingHint.Text = "This controller is linked, but no hardware drawing is selected. Choose a matching shared drawing above.";
                return;
            }
            var hardware = _drawings.FirstOrDefault(x => x.DeviceId == pin.HardwareId);
            if (hardware is null || string.IsNullOrWhiteSpace(hardware.Diagram.Svg))
            {
                DrawingHint.Text = "The selected hardware drawing is not available in the current shared catalog.";
                return;
            }
            var diagram = hardware.Diagram;
            var section = SectionPicker.SelectedItem as string ?? "All";
            var page = SquadronsDrawingProjection.Diagram(snapshot!.Name, diagram, _rows, slot, section);
            _drawingPresenter.Show(diagram, page, SelectDrawingInput, DropDrawingCommand,
                () => StatusText.Text = "Select an existing assignment in the table to clear it.",
                AppearanceManager.ArtworkOpacityFor(this),
                control =>
                {
                    var matches = SquadronsDrawingProjection.ForControl(control, _rows, slot, section);
                    return control.Label + " · " + control.InputKey +
                        (matches.Count == 0 ? "\nNo captured assignments" :
                            "\n" + string.Join("\n", matches.Select(x => x.CommandName + " · " + x.InputLabel)));
                },
                SquadronsCommandDragFormat);
            DrawingHint.Visibility = Visibility.Collapsed;
        }
        private void DrawingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || _session is null || SelectedController is not { } slot ||
                DrawingPicker.SelectedItem is not WorkspaceHardware drawing) return;
            Run(() =>
            {
                _session.ChangeDrawing(slot, drawing.DeviceId);
                Refresh(false);
                StatusText.Text = "Shared drawing selected. Save profile to retain this choice.";
            });
        }
        private void ClearDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || SelectedController is not { } slot) return;
            _session.ChangeDrawing(slot, null);
            Refresh(false);
            StatusText.Text = "Drawing hidden. Save profile to retain this choice.";
        });
        private void FitDrawingClicked(object sender, RoutedEventArgs e) => _drawingViewport.Fit();
        // The Expander may be larger than the pane's arranged row. Clamp the scroll
        // host to the actual resizable row so WPF registers clipped drawing content
        // as scrollable extent instead of simply hiding it behind the window edge.
        private bool _drawingBoundsQueued;
        private void DrawingPanelSizeChanged(object sender, SizeChangedEventArgs e) => QueueDrawingBounds();

        private void QueueDrawingBounds()
        {
            if (_drawingBoundsQueued || DrawingPanel is null || DrawingScroll is null) return;
            _drawingBoundsQueued = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                _drawingBoundsQueued = false;
                if (!DrawingPanel.IsExpanded || DrawingRow.ActualHeight <= 0) return;
                var top = DrawingScroll.TranslatePoint(new Point(0, 0), DrawingPanel).Y;
                var maximum = Math.Max(24, DrawingRow.ActualHeight - DrawingPanel.Margin.Top - top - 4);
                if (double.IsFinite(maximum) && Math.Abs(DrawingScroll.MaxHeight - maximum) > 0.5)
                    DrawingScroll.MaxHeight = maximum;
            }));
        }

        private void DrawingExpanded(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, DrawingPanel) || DrawingRow is null) return;
            // A GridSplitter cannot resize an Auto row; give the open drawing
            // a share of the available height rather than relying on its content size.
            DrawingRow.MinHeight = 150;
            DrawingRow.Height = new GridLength(1, GridUnitType.Star);
            DrawingSplitter.Visibility = Visibility.Visible;
            QueueDrawingBounds();
            if (!_refreshing && _drawingPresenter is not null) Run(RenderDrawing);
        }
        private void DrawingCollapsed(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, DrawingPanel) || DrawingRow is null) return;
            // Return all reclaimed space to the assignment list when artwork is hidden.
            DrawingSplitter.Visibility = Visibility.Collapsed;
            DrawingRow.MinHeight = 0;
            DrawingRow.Height = GridLength.Auto;
            DrawingScroll.MaxHeight = double.PositiveInfinity;
        }
        private void ClickToAssignChanged(object sender, RoutedEventArgs e)
        {
            if (DrawingDropFeedback is null) return;
            DrawingDropFeedback.Text = ClickToAssign.IsChecked == true
                ? "Assignment mode: select a command, then click an outlined hardware control."
                : "Inspection mode: click a control to inspect its existing bindings.";
        }

        private void SelectDrawingInput(HardwareGraphicControl control)
        {
            if (ClickToAssign.IsChecked == true)
            {
                if (CommandPicker.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand selected)
                {
                    DrawingDropFeedback.Text = "Select a command above before clicking a drawing control.";
                    return;
                }
                DropDrawingCommand(control, selected.Id);
                return;
            }
            if (SelectedController is not { } slot) return;
            var section = SectionPicker.SelectedItem as string ?? "All";
            var matches = SquadronsDrawingProjection.ForControl(control, _rows, slot, section);
            if (matches.Count > 0)
            {
                var recordId = matches[0].RecordId;
                BindingsGrid.SelectedItem = BindingsGrid.Items.Cast<SquadronsWorkspaceRow>()
                    .FirstOrDefault(row => row.RecordId == recordId);
                if (BindingsGrid.SelectedItem is not null) BindingsGrid.ScrollIntoView(BindingsGrid.SelectedItem);
            }
            StatusText.Text = control.Label + " · " + control.InputKey +
                (matches.Count == 0 ? " · No captured assignments." :
                    " · " + matches.Count + " captured assignment(s).");
        }
        private void DropDrawingCommand(HardwareGraphicControl control, string commandId)
        {
            try
            {
            if (_session is null || SelectedController is not { } slot)
                throw new InvalidOperationException("Select a linked joystick slot.");
            var command = _backend.GetCatalog(_session.Snapshot.Profile).Commands.Single(x => x.Id == commandId);
            var plan = SquadronsAssignmentPlanner.For(_backend, _session.Snapshot.Profile,
                command.Metadata["squadrons.concept"]);
            var captured = plan.EditableRecords == 1
                ? _backend.ReadBindings(_session.Snapshot.Profile)
                    .Single(x => x.RecordId == plan.EditableRecordIds[0]).Input
                : null;
            var input = SquadronsInputCodec.FromDrawing(control.InputKey, control.IsAxis, captured);
            if (plan.EditableRecords == 1)
            {
                _session.RebindNative(plan.EditableRecordIds[0], slot, input.Key, input.Type, input.Direction);
                Refresh(false);
                StatusText.Text = "Existing Squadrons binding updated in draft. Save before exporting.";
            }
            else
            {
                // No unique replacement target: append only if the backend can
                // verify a native template for the exact command concept.
                _session.AddSquadronsNativeBinding(command.Metadata["squadrons.concept"],
                    slot, input.Key, input.Type, input.Direction);
                Refresh(false);
                StatusText.Text = "New Squadrons binding created in draft. Save before exporting.";
            }
            DrawingDropFeedback.Text = StatusText.Text;
            }
            catch (Exception error)
            {
                DrawingDropFeedback.Text = "Assignment not applied: " + error.Message;
                StatusText.Text = DrawingDropFeedback.Text;
                MessageBox.Show(this, error.Message, "Squadrons assignment not applied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void CommandDragPressed(object sender, MouseButtonEventArgs e)
        {
            _commandDragStart = e.GetPosition(CommandDragHandle);
        }
        private void DragCapturedCommand(object sender, MouseEventArgs e)
        {
            if (!ReferenceEquals(sender, CommandDragHandle) || _commandDragStart is not { } start ||
                e.LeftButton != MouseButtonState.Pressed ||
                CommandPicker.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command ||
                _session is null || _session.AccessMessage.Length != 0)
            {
                if (e.LeftButton != MouseButtonState.Pressed) _commandDragStart = null;
                return;
            }
            var at = e.GetPosition(CommandDragHandle);
            if (Math.Abs(at.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(at.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _commandDragStart = null;
            DrawingDropFeedback.Text = "Drag " + command.DisplayName + " onto the outlined hardware control.";
            var effect = DragDrop.DoDragDrop(CommandDragHandle,
                new DataObject(SquadronsCommandDragFormat, command.Id), DragDropEffects.Copy);
            if (effect == DragDropEffects.None &&
                !DrawingDropFeedback.Text.StartsWith("Drop onto ", StringComparison.Ordinal))
                DrawingDropFeedback.Text = "Drop canceled or outside a supported callout. Aim for an outlined input on the drawing.";
        }
        private void RefreshRows()
        {
            if (_refreshing) return;
            var selected = (BindingsGrid.SelectedItem as SquadronsWorkspaceRow)?.RecordId;
            var review = ReviewCaptured.IsChecked == true;
            var section = SectionPicker.SelectedItem as string ?? "Flight";
            var rows = SquadronsWorkspaceProjection.Filter(_rows, SectionPicker.SelectedItem as string ?? "All", TypePicker.SelectedItem as string ?? "All inputs", Search.Text,
                (DevicePicker.SelectedItem as GameDeviceChoice)?.Id ?? "all", includeUnverified: review);
            if (_focusedConcept is not null) rows = rows.Where(row => row.Native.Concept == _focusedConcept).ToArray();
            BindingsGrid.ItemsSource = rows;
            BindingsGrid.SelectedItem = rows.FirstOrDefault(x => x.RecordId == selected);
            ContextColumn.Visibility = section == "All" ? Visibility.Visible : Visibility.Collapsed;
            ControllerColumn.Visibility = review || (DevicePicker.SelectedItem is GameDeviceChoice { Id: "all" })
                ? Visibility.Visible : Visibility.Collapsed;
            HardwareColumn.Visibility = BindingColumn.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
            CountText.Text = review ? rows.Count + " captured records · " + rows.Count(x => x.HardwareStatus == "Input absent on linked device") + " inputs absent on linked hardware" :
                rows.Count + " captured joystick assignments · " + section + ". Check hardware status in advanced review; saved slot ownership is not physical identity.";
            if (_focusedConcept is not null)
                CountText.Text = "Command: " + _focusedConcept + " · " + rows.Count + " matching captured bindings. " +
                    "Unassigned commands remain selectable in the command browser.";
            EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Text = _session is null ? "Import a Squadrons controls file to get started." : review ? "No matching captured records. Change the context, input filter or search." :
                _scanningInputs ? "Inspecting controller inputs…" : "No captured joystick assignments match these filters. Change the context or input filter, or enable advanced review for preserved native records.";
            if (_focusedConcept is not null && rows.Count == 0)
                EmptyState.Text = "This command has no assignments matching the current controller and input filters. The command is still available in the catalog.";
            UpdateEditor();
            if (DrawingPanel.IsExpanded == true) Run(RenderDrawing);
        }
        private void UpdateEditor()
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            AssignmentEditor.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
            var editable = row?.Editable == true && _session?.AccessMessage.Length == 0;
            TargetDevice.IsEnabled = editable && !_scanningInputs;
            TargetDevice.SelectedItem = TargetDevice.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == row?.DeviceId);
            AssignmentHint.Text = row is null ? "Select an assignment to edit." : editable
                ? row.CommandName + " · " + row.BindingLabel + ". Applying replaces only this record; other assignments remain intact."
                : "This native record is preserved. Editing its input type or combination is not supported yet.";
            UpdateInputChoices();
        }
        private void UpdateInputChoices()
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            var pin = TargetDevice.SelectedItem is GameDeviceChoice target ? _session?.Snapshot.Hardware.GetValueOrDefault(target.Id) : null;
            var keys = _scanningInputs ? null : GameControllerInputs.VerifiedKeys(pin, _controllerInputs);
            InputPicker.ItemsSource = keys is null ? [] : SquadronsInputCodec.Choices.Where(x => keys.Contains(x.PhysicalKey, StringComparer.Ordinal)).ToArray();
            InputPicker.SelectedItem = InputPicker.Items.Cast<SquadronsInput>().FirstOrDefault(x => x == row?.Native.Input);
            InputPicker.IsEnabled = row?.Editable == true && TargetDevice.IsEnabled && keys is not null;
            UpdateApplyAvailability();
            if (row?.Editable == true)
                AssignmentHint.Text = keys is null ? "Link the destination captured slot to a connected controller and Rescan to verify its real inputs. No generic controls can be assigned." :
                    row.CommandName + " · " + row.BindingLabel + ". Only enumerated controls on the destination device are offered." +
                    (row.HardwareStatus == "Input absent on linked device" ? " The captured input does not exist in the linked device's verified controls; its native record is preserved for review." : "");
        }
        private void UpdateApplyAvailability() => ApplyButton.IsEnabled = InputPicker.IsEnabled && InputPicker.SelectedItem is SquadronsInput && _session?.AccessMessage.Length == 0;
        private void TargetChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) Run(UpdateInputChoices); }
        private void InputChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateApplyAvailability(); }
        private void UpdateAvailability()
        {
            if (_refreshing || SaveState is null) return;
            SaveButton.IsEnabled = DiscardButton.IsEnabled = Pending;
            ExportButton.IsEnabled = _session is not null && !Pending && _session.AccessMessage.Length == 0;
            ProfileName.IsEnabled = _session is not null;
            SaveState.Text = _session is null ? "No profile" : Pending ? "Unsaved changes" : "Saved";
        }
        /// <summary>Only native joystick slots support physical linking and slot diagnostics.</summary>
        private string? SelectedController => DevicePicker.SelectedItem is GameDeviceChoice choice &&
            choice.Id.StartsWith("squadrons.joystick.", StringComparison.Ordinal) ? choice.Id : null;
        /// <summary>The catalog filters any selected native source, including read-only system devices.</summary>
        private string? SelectedAssignmentSource => (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
        private void RescanControllers()
        {
            _identification.Stop();
            var observed = _services.Discover();
            ConnectedPicker.ItemsSource = observed.Where(x => x.Connected && x.PhysicalGuid != Guid.Empty && x.ProductGuid != Guid.Empty && EditionAccess.Policy.AllowsDevice(x))
                .GroupBy(x => x.PhysicalGuid).Where(x => x.Count() == 1).Select(x => x.Single()).ToArray();
            ConnectedPicker.SelectedIndex = -1; UpdateControllerAvailability();
        }
        private void UpdateControllerAvailability()
        {
            var selected = SelectedController;
            var ready = selected is not null && !_scanningInputs && _session?.CanLinkCapturedDevices == true && _session.AccessMessage.Length == 0;
            var running = _identification.Running;
            var pin = selected is null ? null : _session?.Snapshot.Hardware.GetValueOrDefault(selected);
            RescanControllerButton.IsEnabled = !running && !_scanningInputs;
            ConnectedPicker.IsEnabled = ready && !running;
            IdentifyControllerButton.IsEnabled = ready && !running && ConnectedPicker.Items.Count > 0;
            CancelIdentificationButton.IsEnabled = running;
            LinkControllerButton.IsEnabled = ready && !running && ConnectedPicker.SelectedItem is DetectedDcsDevice;
            UnlinkControllerButton.IsEnabled = ready && !running && pin is not null;
            RemoveUnusedSlotButton.IsEnabled = ready && !running && pin is null;
            ClearSlotButton.IsEnabled = ready && !running && _rows.Any(x => x.Native.Type == 3 && x.DeviceId == selected);
            LinkStatus.Text = selected is null ? "Select a captured slot to manage its physical link." : pin is null ? "Unlinked captured slot" :
                "Linked: " + pin.Device.Name + "\n" + pin.Device.PhysicalGuid + "\n" +
                (GameInputReadiness.Ready(pin, _services.Discover()) ? "Connected · exact instance and product" : "Unavailable · disconnected or identity changed. Rescan after reconnecting.");
            if (selected is not null && _session is not null)
                LinkStatus.Text += "\n" + SquadronsWorkspaceProjection.SlotDiagnostic(_session.Snapshot.Profile, _rows, selected);
            var systemSelection = DevicePicker.SelectedItem is GameDeviceChoice { Id: var systemId }
                && systemId.StartsWith("system.", StringComparison.Ordinal);
            ControllerSummary.Text = systemSelection ? "Native system assignments · read-only. Codes are shown exactly as captured." :
                selected is null ? "Select a controller." : pin is null ? "Not linked · open Controller setup." :
                pin.Device.Name + (GameInputReadiness.Ready(pin, _services.Discover()) ? " · Connected" : " · Unavailable");
        }
        private void CaptureReviewChanged(object sender, RoutedEventArgs e) { if (!_refreshing) RefreshRows(); }
        private void SetupCollapsed(object sender, RoutedEventArgs e) { if (!_refreshing && ReferenceEquals(e.OriginalSource, ControllerSetup)) _identification.Stop(); }
        private void ClearIdentification()
        {
            IdentificationStatus.Text = ""; ConnectedPicker.SelectedIndex = -1;
            if (!_refreshing) Run(UpdateControllerAvailability);
        }
        private void ShowIdentification(IReadOnlyDictionary<DetectedDcsDevice, string> activity)
        {
            ConnectedPicker.SelectedIndex = -1;
            if (activity.Count != 1) { IdentificationStatus.Text = "Several controllers moved. Operate just one physical unit."; return; }
            var detected = activity.Single();
            _identification.Stop();
            ConnectedPicker.SelectedItem = ConnectedPicker.Items.Cast<DetectedDcsDevice>().SingleOrDefault(x => x.PhysicalGuid == detected.Key.PhysicalGuid);
            IdentificationStatus.Text = detected.Key.Name + " · " + detected.Value + "\n" + detected.Key.PhysicalGuid + "\nPhysical unit identified. Verify its captured slot in Squadrons, then review the link.";
            UpdateControllerAvailability();
        }
        private void ReviewLink(GameControllerChange change)
        {
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session!.ApplyControllerChange(change); Refresh(false); StatusText.Text = "Physical link staged. Native slots and bindings are unchanged. Save to keep this link.";
        }
        private void LinkClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!LinkControllerButton.IsEnabled || SelectedController is not { } id || ConnectedPicker.SelectedItem is not DetectedDcsDevice device) return;
            ReviewLink(_session!.PreviewCapturedDeviceLink(id, device.PhysicalGuid));
        });
        private void UnlinkClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (UnlinkControllerButton.IsEnabled && SelectedController is { } id) ReviewLink(_session!.PreviewCapturedDeviceUnlink(id));
        });
        private void ReviewNativeChange(GameControllerChange change)
        {
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session!.ApplyControllerChange(change);
            Refresh(false);
            if (change.ResultDeviceId is { } id)
                DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == id);
            StatusText.Text = "Native Squadrons assignments / slots updated in the draft. Save before exporting a review copy.";
        }
        private void ClearSlotClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (ClearSlotButton.IsEnabled && SelectedController is { } id)
                ReviewNativeChange(_session!.PreviewClearCapturedAssignments(id));
        });
                private void RemoveUnusedSlotClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!RemoveUnusedSlotButton.IsEnabled || SelectedController is not { } id) return;
            var change = _session!.PreviewRemoveUnusedCapturedDevice(id);
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session.ApplyControllerChange(change);
            Refresh(false);
            StatusText.Text = "Unused slot removed from staged profile. Save before exporting.";
        });
        private void IdentifyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!IdentifyControllerButton.IsEnabled) return;
            _identification.Start(_services.Discover()); UpdateControllerAvailability();
            IdentificationStatus.Text = "Press a button, move a hat or move an axis noticeably on one controller. Held switches are ignored until pressed again.";
        });
        private void StopIdentificationClicked(object sender, RoutedEventArgs e) => _identification.Stop();
        private async void RescanClicked(object sender, RoutedEventArgs e)
        {
            if (!RescanControllerButton.IsEnabled) return;
            Run(RescanControllers); await RefreshControllerInputsAsync();
        }
        private void ConnectedChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) Run(UpdateControllerAvailability); }
        private void CommandChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            var command = CommandPicker.SelectedItem as Heimdall.Domain.MultiSim.SimCommand;
            if (command is null || _session is null)
            {
                CommandHint.Text = "Choose a command to inspect its saved binding records.";
                return;
            }
            var plan = SquadronsAssignmentPlanner.For(_backend, _session.Snapshot.Profile,
                command.Metadata["squadrons.concept"]);
            CommandHint.Text = CommandPicker.Items.Count + " catalog commands (reference + imported) · " +
                command.DisplayName + " · " + command.Category + " · " +
                plan.CapturedRecords + " captured records · " + plan.EditableRecords +
                " supported replacement records. " + (plan.CanReplaceCapturedRecord
                    ? "Drag a command onto a compatible drawing input to replace the sole editable record, or select an assignment below."
                    : "Drag onto a compatible drawing input to create a record when this command has a verified native template; unknown templates remain blocked.");
        }
        private void ShowCommandClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (CommandPicker.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command) return;
            SectionPicker.SelectedItem = command.Category;
            ReviewCaptured.IsChecked = true;
            _focusedConcept = command.Metadata["squadrons.concept"];
            RefreshRows();
            var match = _rows.FirstOrDefault(row => row.Native.Concept == _focusedConcept
                && (SelectedController is null || row.DeviceId == SelectedController));
            if (match is not null)
                BindingsGrid.SelectedItem = BindingsGrid.Items.Cast<SquadronsWorkspaceRow>()
                    .FirstOrDefault(row => row.RecordId == match.RecordId);
            if (BindingsGrid.SelectedItem is not null) BindingsGrid.ScrollIntoView(BindingsGrid.SelectedItem);
            else CommandHint.Text = "No captured assignment matches this command and controller. No new native records were written.";
        });
        private void EditSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (BindingsGrid.SelectedItem is SquadronsWorkspaceRow)
                UpdateEditor();
        }
        private void ClearSelectedClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Native.Type: 3 } row)
                throw new InvalidOperationException("Choose an existing joystick assignment to clear.");
            ReviewNativeChange(_session!.PreviewClearCapturedRecord(row.RecordId));
        });
        private void ApplyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Editable: true } row || TargetDevice.SelectedItem is not GameDeviceChoice device || InputPicker.SelectedItem is not SquadronsInput input)
                throw new InvalidOperationException("Choose an editable binding, captured controller and input.");
            _session!.RebindNative(row.RecordId, device.Id, input.Key, input.Type, input.Direction);
            Refresh(false); StatusText.Text = "Assignment staged. Save before exporting.";
        });
        private void ImportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var dialog = new OpenFileDialog { Title = "Import Squadrons controls", Filter = "Squadrons controls|ProfileOptions_profile_synced|All files|*.*" };
            if (dialog.ShowDialog(this) == true) ImportFile(dialog.FileName);
        });
        private void ExportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder for a separate Squadrons review copy" };
            if (dialog.ShowDialog(this) == true) ExportReview(dialog.FolderName);
        });
        private void SaveClicked(object sender, RoutedEventArgs e) => SaveProfile();
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _identification.Stop();
            _session?.Discard(); Refresh();
            if (_revision is null) { _session = null; Refresh(); }
        }
        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || ProfilePicker.SelectedItem is not GameWorkspaceEntry entry || entry.Id == _session?.Snapshot.Id) return;
            if (ConfirmLeave()) Run(() => LoadProfile(entry.Id));
            _refreshing = true; ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id); _refreshing = false;
        }
        private void GameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || GamePicker.SelectedItem is not ComboBoxItem { Tag: string game } || game == "star-wars-squadrons") return;
            if (!ConfirmLeave()) { _refreshing = true; GamePicker.SelectedIndex = 2; _refreshing = false; return; }
            RequestedGame = game; _allowClose = true; Close();
        }
        private bool ConfirmLeave()
        {
            if (!Pending) return true;
            var choice = _pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save changes to this Squadrons profile?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes) return SaveProfile();
            if (choice != MessageBoxResult.No) return false;
            _session?.Discard(); return true;
        }
        private void ConfirmClose(object? sender, CancelEventArgs e) { _identification.Stop(); if (!_allowClose) e.Cancel = !ConfirmLeave(); }
        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            if (ReferenceEquals(sender, DevicePicker))
            {
                _identification.Stop();
                if (DevicePicker.SelectedItem is GameDeviceChoice { Id: var id } &&
                    id.StartsWith("system.", StringComparison.Ordinal))
                {
                    SectionPicker.SelectedItem = "All";
                    TypePicker.SelectedItem = "All inputs";
                    ControllerSetup.IsExpanded = false;
                }
                Run(UpdateControllerAvailability);
                RefreshCommandCatalog(_session?.Snapshot);
                Run(RenderDrawing);
            }
            if (ReferenceEquals(sender, SectionPicker) || ReferenceEquals(sender, TypePicker))
                _focusedConcept = null;
            RefreshRows();
        }
        private void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) RefreshRows(); }
        private void BindingChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateEditor(); }
        private void NameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) UpdateAvailability(); }
        private void Run(Action action) { try { action(); } catch (Exception error) { StatusText.Text = error.Message; } }
        #endregion END private helpers
    }
}
