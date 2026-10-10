using System.IO;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Rendering;
using Microsoft.Win32;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DcsIpia.Presentation
{
    /// <summary>Edits locally persisted module controls on canonical draw.io generated SVGs.</summary>
    public partial class ModuleWorkspaceWindow : Window
    {
        private readonly ModuleWorkspaceViewModel _viewModel;
        private readonly ILogger<ModuleWorkspaceWindow> _logger;
        private readonly IAppSettingsService? _settings;
        private readonly IEditorWorkspaceRepository? _repository;
        private EditorWorkspaceSnapshot? _saved;
        private DraftAssignmentService? _draft;
        private EditorWorkspaceSnapshot? _working;
        private readonly IConnectedDeviceDiscovery _deviceDiscovery;
        private readonly HardwareInventoryManager? _inventory;
        private readonly IUserMappingLibrary? _userMappings;
        private readonly Func<MappingRevisionReview, bool>? _mappingReviewConfirmation;
        private bool _genericPagingUpdating;
        private readonly Func<bool, IReadOnlyList<string>?>? _commandFilePicker;
        private readonly Func<bool, string?>? _outputPathPicker;
        private readonly Func<string?>? _documentationFolderPicker;
        private readonly Func<string?>? _completeReleaseFolderPicker;
        private readonly Func<string?>? _integrationFilePicker;
        private readonly Func<string?>? _axisDefaultsFilePicker;
        private string _savedGamesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        private bool _discoveryBusy;
        private bool _structureDirty;
        private bool _managementRefreshing;
        private bool _authoringRefreshing;
        private readonly List<ModuleSummaryCard> _summaryCards = [];
        private Point? _summaryDragStart;
        private ModuleSummaryCard? _summaryDragCard;
        private Point? _pageDragStart;
        private KneeboardPage? _pageDragPage;
        private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextBlock> _calloutText = new(StringComparer.Ordinal);
        private HardwareGraphicControl? _selected;
        private Point? _dragStart;
        private bool _updatingInspector;
        private bool _updatingFilters;
        private bool _building;
        private AssignmentLayer? _categoryScope;
        private readonly Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? _inputSourceFactory;
        private readonly Action<string, string>? _inputLogClipboardWriter;
        private readonly System.Windows.Threading.DispatcherTimer _inputTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private IDeviceInputSource? _inputSource;
        private InputTestSession? _inputSession;
        private Guid? _inputFeedbackDevice;
        private GeneratedInputTestPlan? _inputPlan;
        private readonly Dictionary<(Guid Device, string Key), DateTime> _liveAxisUntil = [];
        private readonly HashSet<(Guid Device, string Key)> _livePressedInputs = [];
        private GridLength _inputTestPaneHeight = new(1, GridUnitType.Star);
        private readonly Dictionary<Border, Brush> _testHighlights = [];
        private readonly Dictionary<string, Key> _testKeyboard = new(StringComparer.Ordinal);
        private readonly InputTestController _inputTest = new();
        /// <summary>Opens a saved module editor with a SQLite persistence boundary.</summary>
        /// <param name="viewModel">Persisted editor data or a source preview.</param>
        /// <param name="settings">Occupied destination preference.</param>
        /// <param name="repository">Local editor database.</param>
        /// <param name="deviceDiscovery">Optional native device discovery adapter.</param>
        /// <param name="commandFilePicker">Optional command-file selection adapter.</param>
        /// <param name="outputPathPicker">Optional snapshot file or package folder selection adapter.</param>
        /// <param name="documentationFolderPicker">Optional parent folder selection for documentation export.</param>
        /// <param name="completeReleaseFolderPicker">Optional parent folder selection for Complete release export.</param>
        /// <param name="integrationFilePicker">Optional JSON or Markdown integration instruction selection adapter.</param>
        /// <param name="axisDefaultsFilePicker">Optional cleared-axis DCS profile selection adapter.</param>
        /// <param name="inputSourceFactory">Optional foreground device-input adapter for testing.</param>
        /// <param name="userMappings">Profile-scoped immutable custom mapping revisions.</param>
        /// <param name="inventory">Profile-scoped reviewed hardware choices.</param>
        /// <param name="mappingReviewConfirmation">Optional drawing review consent adapter for desktop tests.</param>
        /// <param name="inputLogClipboardWriter">Optional HTML and plain-text clipboard adapter for testing.</param>
        /// <param name="logger">Application logger for saved-module output failures.</param>
        public ModuleWorkspaceWindow(ModuleWorkspaceViewModel viewModel, IAppSettingsService? settings = null,
                                     IEditorWorkspaceRepository? repository = null, IConnectedDeviceDiscovery? deviceDiscovery = null,
                                     Func<bool, IReadOnlyList<string>?>? commandFilePicker = null, Func<bool, string?>? outputPathPicker = null,
                                     Func<string?>? documentationFolderPicker = null, Func<string?>? completeReleaseFolderPicker = null,
                                     Func<string?>? integrationFilePicker = null, Func<string?>? axisDefaultsFilePicker = null,
                                     Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputSourceFactory = null,
                                     Action<string, string>? inputLogClipboardWriter = null, HardwareInventoryManager? inventory = null, IUserMappingLibrary? userMappings = null, Func<MappingRevisionReview, bool>? mappingReviewConfirmation = null,
                                     ILogger<ModuleWorkspaceWindow>? logger = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger ?? NullLogger<ModuleWorkspaceWindow>.Instance;
            _inventory = inventory;
            _userMappings = userMappings;
            _mappingReviewConfirmation = mappingReviewConfirmation;
            _settings = settings;
            _repository = repository;
            _deviceDiscovery = deviceDiscovery ?? new WindowsConnectedDeviceDiscovery();
            _commandFilePicker = commandFilePicker;
            _outputPathPicker = outputPathPicker;
            _documentationFolderPicker = documentationFolderPicker;
            _completeReleaseFolderPicker = completeReleaseFolderPicker;
            _integrationFilePicker = integrationFilePicker;
            _axisDefaultsFilePicker = axisDefaultsFilePicker;
            _inputSourceFactory = inputSourceFactory;
            _inputLogClipboardWriter = inputLogClipboardWriter;
            _saved = viewModel.Workspace;
            _working = _saved;
            _draft = viewModel.Draft;
            foreach (var pair in viewModel.Labels)
                _labels[pair.Key] = pair.Value;
            InitializeComponent();
            DataContext = _viewModel;
            WorkspaceSettingsButton.IsEnabled = _settings is not null;
            KneeboardList.SelectedIndex = 0;
            DeviceChoice.ItemsSource = _viewModel.UiDiagrams;
            Layers.ItemsSource = _viewModel.ModuleModifierChoices;
            Layers.SelectedIndex = 0;
            Scope.SelectedIndex = 0;
            CommandType.SelectedIndex = 0;
            BindingState.SelectedIndex = 0;
            DeviceChoice.SelectedIndex = _viewModel.UiDiagrams.Count > 0 ? 0 : -1;
            if (_viewModel.HasErrors)
                ImportedTabs.SelectedItem = DiagnosticsTab;
            DevicesScope.SelectedIndex = 0;
            ModifiersScope.SelectedIndex = 0;
            ModifierModeChoice.ItemsSource = Enum.GetValues<ModifierMode>();
            ModifierModeChoice.SelectedIndex = 0;
            RefreshManagement();
            AuthoredPageKind.ItemsSource = Enum.GetValues<KneeboardPageKind>();
            AuthoredPageKind.SelectedIndex = 0;
            SummaryCardAccent.ItemsSource = new[] { "cyan", "gold", "red", "blue", "orange", "purple", "green" };
            SummaryCardAccent.SelectedIndex = 0;
            RefreshAuthoring();
            RefreshDeviceChoices();
            Loaded += async (_, _) => { if (_working is not null) { await LoadInventoryChoicesAsync(); await LoadUserMappingsAsync(); await RefreshDetectedDevicesAsync(); } };
            Closing += ConfirmClose;
            _inputTest.StatusChanged += status => { InputTestStatus.Text = status; };
            _inputTest.HighlightsChanged += () => HighlightInputControls();
            _inputTimer.Tick += PollInputTest;
            Deactivated += (_, _) => StopInputTest("Stopped on focus loss; restart to clear modifier state.");
            Closed += (_, _) => StopInputTest("Stopped.");
            RefreshEditor();
            UpdateBuildAvailability();
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void ClearInputTestLogClicked(object sender, RoutedEventArgs e)
        {
            InputTestLog.Items.Clear();
            InputTestLogMessage.Text = "Log cleared.";
        }
        private void CopyInputTestLogClicked(object sender, RoutedEventArgs e)
        {
            var rows = InputTestLogFormatter.SelectRows(InputTestLog.Items.Cast<InputTestLogEntry>().ToArray(),
                InputTestLog.SelectedItems.Cast<InputTestLogEntry>().ToArray());
            if (rows.Count == 0) return;
            try
            {
                var content = InputTestLogFormatter.Format(rows);
                if (_inputLogClipboardWriter is not null) _inputLogClipboardWriter(content.Html, content.Text);
                else
                {
                    var data = new DataObject();
                    data.SetData(DataFormats.Html, content.Html);
                    data.SetData(DataFormats.UnicodeText, content.Text);
                    Clipboard.SetDataObject(data, true);
                }
                InputTestLogMessage.Text = $"Copied {rows.Count} event{(rows.Count == 1 ? "" : "s")}.";
            }
            catch (Exception ex) { InputTestLogMessage.Text = "Could not copy events to the clipboard: " + ex.Message; }
        }
        private void InputTestPaneExpanded(object sender, RoutedEventArgs e)
        {
            if (InputTestRow is null || InputTestSplitter is null) return;
            InputTestRow.MinHeight = 60;
            InputTestRow.Height = _inputTestPaneHeight;
            InputTestSplitter.Visibility = Visibility.Visible;
        }
        private void InputTestPaneCollapsed(object sender, RoutedEventArgs e)
        {
            if (InputTestRow is null || InputTestSplitter is null) return;
            _inputTestPaneHeight = InputTestRow.Height;
            InputTestRow.MinHeight = 0;
            InputTestRow.Height = GridLength.Auto;
            InputTestSplitter.Visibility = Visibility.Collapsed;
        }
        private void StartInputTestClicked(object sender, RoutedEventArgs e)
        {
            StopInputTest("Preparing generated Lua…");
            try
            {
                var snapshot = InputTestSnapshotChoice.SelectedIndex == 1 ? _saved ?? throw new InvalidOperationException("No saved profile.") : EditSnapshot();
                var plan = GeneratedInputTestPlan.Create(snapshot);
                var required = plan.Bindings.Select(x => x.PhysicalGuid).Concat(plan.Modifiers.Select(x => x.PhysicalGuid))
                    .Where(x => x != Guid.Empty).Distinct().ToArray();
                var attached = _inputSourceFactory is null ? _deviceDiscovery.Discover().Where(x => x.Connected).Select(x => x.PhysicalGuid).ToHashSet() : required.ToHashSet();
                var devices = required.Where(attached.Contains).ToArray();
                _inputSource = _inputSourceFactory?.Invoke(new System.Windows.Interop.WindowInteropHelper(this).Handle, devices) ??
                    new WindowsDeviceInputSource(new System.Windows.Interop.WindowInteropHelper(this).Handle, devices);
                _inputPlan = plan;
                _inputSession = new(plan.Bindings, plan.Modifiers);
                _testKeyboard.Clear();
                var diagnostics = new List<string>(plan.Diagnostics);
                diagnostics.AddRange(required.Where(x => !attached.Contains(x)).Select(x => "Missing controller/modifier device: " + x.ToString("D")));
                foreach (var modifier in plan.Modifiers.Where(x => x.PhysicalGuid == Guid.Empty))
                {
                    if (InputTestController.KeyboardTestKey(modifier.InputKey) is { } key) _testKeyboard[modifier.InputKey] = key;
                    else diagnostics.Add("Unsupported keyboard modifier: " + modifier.InputKey);
                }
                var supported = _inputSource.SupportedInputs;
                foreach (var modifier in plan.Modifiers.Where(x => x.PhysicalGuid != Guid.Empty && supported.ContainsKey(x.PhysicalGuid)))
                    if (!supported[modifier.PhysicalGuid].Any(x => DcsInputKey.SamePhysicalInput(x, modifier.InputKey)))
                        diagnostics.Add("Unsupported modifier input: " + modifier.Name + " / " + modifier.InputKey);
                foreach (var binding in plan.Bindings.Where(x => supported.ContainsKey(x.PhysicalGuid)))
                    if (!supported[binding.PhysicalGuid].Any(x => DcsInputKey.SamePhysicalInput(x, binding.Binding.InputKey)))
                        diagnostics.Add("Unsupported mapped input: " + binding.Binding.InputKey + " / " + binding.PhysicalGuid.ToString("D"));
                InputTestLog.Items.Clear();
                InputTestLogMessage.Text = "";
                InputTestStatus.Text = $"Testing frozen {(InputTestSnapshotChoice.SelectedIndex == 1 ? "saved profile" : "current edits")} at {DateTime.Now:T} · {devices.Length} controllers. Changes only; startup inputs establish a baseline. Selected-device feedback; modifiers from all controllers. Stop/Start after edits. Generated mappings only; DCS defaults/execution are unverified." +
                    (diagnostics.Count == 0 ? "" : "\nPartial test — " + string.Join("; ", diagnostics.Distinct()));
                StartInputTestButton.IsEnabled = false;
                StopInputTestButton.IsEnabled = true;
                InputTestSnapshotChoice.IsEnabled = false;
                _inputTimer.Start();
                PollInputTest(this, EventArgs.Empty);
            }
            catch (Exception ex) { StopInputTest("Input test could not start: " + ex.Message); }
        }
        private void StopInputTestClicked(object sender, RoutedEventArgs e) => StopInputTest("Stopped. Start again to capture a fresh generated profile.");
        private void StopInputTest(string status)
        {
            _inputTimer.Stop();
            _inputSession?.Reset(); _inputSession = null; _inputPlan = null;
            _inputFeedbackDevice = null;
            _inputSource?.Dispose(); _inputSource = null;
            _testKeyboard.Clear(); _liveAxisUntil.Clear(); _livePressedInputs.Clear();
            ClearInputHighlights();
            if (StartInputTestButton is null) return;
            StartInputTestButton.IsEnabled = _saved is not null;
            StopInputTestButton.IsEnabled = false;
            InputTestSnapshotChoice.IsEnabled = true;
            InputTestStatus.Text = status;
            InputTestModifiers.Text = "";
        }
        private void PollInputTest(object? sender, EventArgs e)
        {
            if (_inputSource is null || _inputSession is null || _inputPlan is null) return;
            try
            {
                var frames = _inputSource.Read().ToList();
                frames.Add(new(Guid.Empty, _testKeyboard.ToDictionary(x => x.Key, x => Keyboard.IsKeyDown(x.Value) ? 1.0 : 0.0)));
                var physicalDevice = CurrentInputTestDevice();
                if (_inputFeedbackDevice != physicalDevice)
                {
                    InputTestLog.Items.Clear();
                    InputTestLogMessage.Text = "";
                    _liveAxisUntil.Clear();
                    _livePressedInputs.Clear();
                    _inputFeedbackDevice = physicalDevice;
                }
                var results = _inputSession.ProcessDevice(frames, physicalDevice);
                foreach (var result in results)
                {
                    var commands = result.PhysicalGuid == Guid.Empty ? "Keyboard modifier input (keyboard commands are not generated)" :
                        string.Join(", ", result.Commands.Select(x => x.Name + (x.IsAxis ? " (axis)" : "")));
                    var entry = new InputTestLogEntry(DateTime.Now, result.PhysicalGuid, result.InputKey, result.Value, commands);
                    InputTestLog.Items.Add(entry);
                    if (InputTestLog.Items.Count > 200) InputTestLog.Items.RemoveAt(0);
                    InputTestLog.ScrollIntoView(entry);
                    if (result.Value > 0.5 && !result.IsAxis) _livePressedInputs.Add((result.PhysicalGuid, result.InputKey));
                    else if (!result.IsAxis) _livePressedInputs.Remove((result.PhysicalGuid, result.InputKey));
                    if (result.IsAxis) _liveAxisUntil[(result.PhysicalGuid, result.InputKey)] = DateTime.Now.AddMilliseconds(150);
                }
                var now = DateTime.Now;
                foreach (var key in _liveAxisUntil.Where(x => x.Value < now).Select(x => x.Key).ToArray())
                    _liveAxisUntil.Remove(key);
                HighlightInputControls();
                var active = _inputSession.ActiveModifiers.Select(x => x.Name).Distinct().ToArray();
                InputTestModifiers.Text = active.Length == 0 ? "" : "Active modifiers: " + string.Join(", ", active);
            }
            catch (Exception ex)
            {
                StopInputTest("Input test stopped: " + ex.Message);
            }
        }
        private void ClearInputHighlights()
        {
            foreach (var pair in _testHighlights)
                pair.Key.BorderBrush = pair.Value;
            _testHighlights.Clear();
        }
        private void HighlightInputControls()
        {
            ClearInputHighlights();
            if (_inputFeedbackDevice is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;
            var graphic = diagram.Graphic;
            if (graphic is null) return;
            foreach (var control in graphic.Children.OfType<Border>())
            {
                if (control.Tag is not HardwareGraphicControl hgc) continue;
                var key = (diagram.PhysicalGuid, hgc.InputKey);
                var pressed = _livePressedInputs.Contains(key) || _liveAxisUntil.ContainsKey(key);
                if (!pressed) continue;
                _testHighlights[control] = control.BorderBrush;
                control.BorderBrush = Brushes.Lime;
            }
        }
        private Guid? CurrentInputTestDevice()
        {
            if (DeviceChoice.SelectedItem is UiLayerDiagram diagram) return diagram.PhysicalGuid;
            return null;
        }
        private static Key? KeyboardTestKey(string name) => name switch
        {
            "LShift" => Key.LeftShift, "RShift" => Key.RightShift,
            "LCtrl" => Key.LeftCtrl, "RCtrl" => Key.RightCtrl,
            "LAlt" => Key.LeftAlt, "RAlt" => Key.RightAlt,
            "LWin" => Key.LWin, "RWin" => Key.RWin,
            "Enter" => Key.Return, "Backspace" => Key.Back,
            _ => Enum.TryParse<Key>(name, true, out var key) && key != Key.None ? key : null
        };
        private IReadOnlyList<CommandPaletteEntry> AircraftPalette() => CurrentLayer == AssignmentLayer.Module
            ? _working?.ModuleCommands ?? _viewModel.ModuleCommands
            : _working is null ? _viewModel.UiCommands : CommandPalette.FromUiLayer(_working.UiCommands);
        private void RefreshAuthoring()
        {
            if (_working is null || _authoringRefreshing) return;
            _authoringRefreshing = true;
            try
            {
                AuthoredCommands.ItemsSource = _working.ModuleCommands.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
                AuthoredPages.ItemsSource = _working.KneeboardPages.ToArray();
                SummaryCards.ItemsSource = _summaryCards.ToArray();
                IntegrationInstructions.ItemsSource = _working.IntegrationInstructions.ToArray();
            }
            finally { _authoringRefreshing = false; }
        }
        // ... remaining methods unchanged from original ...
    }
}
