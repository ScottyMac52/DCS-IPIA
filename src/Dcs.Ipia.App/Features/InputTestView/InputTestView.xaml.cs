using System.Windows.Input;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Rendering;
using DcsIpia.Presentation;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DcsIpia.Features.InputTest
{

    /// <summary>Displays live input feedback for a frozen generated mapping plan.</summary>
    public partial class InputTestView : UserControl, IDisposable
    {
        private IModuleWorkspaceContext? _context;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private IDeviceInputSource? _inputSource;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        private InputTestSession? _session;
        private GeneratedInputTestPlan? _plan;
        private readonly List<(string InputKey, string Control, Border Highlight)> _callouts = [];
        private readonly Dictionary<Border, string> _highlightColors = [];
        private UiLayerDiagram? _currentDiagram;
        private InputTestDiagramModel? _currentDiagramModel;
        private Image? _currentArtwork;
        private string? _lastModifierSignature;
        private InputTestLegendItem[] _legendItems = [];
        private EditorWorkspaceSnapshot? _testSnapshot;
         private readonly List<InputTestLogEntry> _events = [];
        private readonly Dictionary<string, System.Windows.Input.Key> _testKeyboard = new(StringComparer.Ordinal);
        private readonly Dictionary<(Guid Device, string Key), DateTime> _liveAxisUntil = [];
        private readonly HashSet<(Guid Device, string Key)> _livePressedInputs = [];
        private Guid? _inputFeedbackDevice;
        private Button? StartButton => ActionBar.FindButton(this, "Start test");
        private Button? StopButton => ActionBar.FindButton(this, "Stop test");
        private readonly DiagramViewport _viewport;

        #region BEGIN Public
        public InputTestView()
        {
            AppearanceManager.ObserveArtwork(this, () => { _lastModifierSignature = null; RefreshLiveArtwork(); });
            InitializeComponent();
            _viewport = new DiagramViewport(DiagramScroll, Diagram, Zoom, "input-test.zoom");
            _timer.Tick += Poll;
            Unloaded += (_, _) => Stop("Stopped.");
            Loaded += OnLoaded;
        }

        /// <summary>Stops live input polling and releases the input source.</summary>
        public void Dispose()
        {
            Stop("Stopped.");
            _timer.Tick -= Poll;
            GC.SuppressFinalize(this);
        }

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            StatusText.Text = context.Working is null
                ? "No working snapshot"
                : $"Input test · {context.Working.Configuration.Module.DisplayName}";
            StartButton?.IsEnabled = context.Working is not null && EditionAccess.Policy.IsOwner;

            if (context.Working is not null)
            {
                var devices = WorkspaceStructureEditor.Describe(context.Working)
                    .Devices.Where(x => x.Layer == AssignmentLayer.Module).ToArray();
                FeedbackDevice.ItemsSource = devices;
                FeedbackDevice.SelectedIndex = 0;
                FeedbackDevice.SelectionChanged += (_, _) => RenderDiagram();
            }

            var window = Window.GetWindow(this);
            if (window is not null)
            {
                window.Deactivated += (_, _) => Stop("Stopped on focus loss; restart to clear modifier state.");
                window.Closed += (_, _) => Stop("Stopped.");
            }
            RenderDiagram();
        }
        #endregion END Public

        #region BEGIN private helpers
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            var window = Window.GetWindow(this);
            if (window is null) return;
            window.Deactivated += (_, _) => Stop("Stopped on focus loss; restart to clear modifier state.");
            window.Closed += (_, _) => Stop("Stopped.");
        }

        private void StartInputTestClicked(object sender, RoutedEventArgs e)
        {
            Stop("Preparing generated Lua…");
            try
            {
                EditionAccess.Policy.RequireOwner();
                var snapshot = InputTestSnapshotChoice.SelectedIndex == 1 ? _context!.Saved ?? throw new InvalidOperationException("No saved profile.") : (_context!.Working! with { Assignments = _context.Draft?.Snapshot ?? _context.Working!.Assignments, Labels = _context.Labels });
                var plan = GeneratedInputTestPlan.Create(snapshot);
                var required = plan.Bindings.Select(x => x.PhysicalGuid).Concat(plan.Modifiers.Select(x => x.PhysicalGuid))
                    .Where(x => x != Guid.Empty).Distinct().ToArray();
                var attached = _context!.InputSourceFactory is null ? _context!.DeviceDiscovery.Discover().Where(x => x.Connected).Select(x => x.PhysicalGuid).ToHashSet() : required.ToHashSet();
                var devices = required.Where(attached.Contains).ToArray();
                _inputSource = _context!.InputSourceFactory?.Invoke(new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(this)).EnsureHandle(), devices) ??
                    new WindowsDeviceInputSource(new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(this)).EnsureHandle(), devices);
                _plan = plan;
                _session = new(plan.Bindings, plan.Modifiers);
                _testSnapshot = snapshot;
                RenderDiagram();
                _testKeyboard.Clear();
                var diagnostics = new List<string>(plan.Diagnostics);
                diagnostics.AddRange(required.Where(x => !attached.Contains(x)).Select(x => "Missing controller/modifier device: " + x.ToString("D")));
                foreach (var modifier in plan.Modifiers.Where(x => x.PhysicalGuid == Guid.Empty))
                {
                    if (KeyboardTestKey(modifier.InputKey) is { } key) _testKeyboard[modifier.InputKey] = key;
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
                _events.Clear();
                InputTestLogMessage.Text = "";
                InputTestStatus.Text = $"Testing frozen {(InputTestSnapshotChoice.SelectedIndex == 1 ? "saved profile" : "current edits")} at {DateTime.Now:T} · {devices.Length} controllers. Changes only; startup inputs establish a baseline. Selected-device feedback; modifiers from all controllers. Stop/Start after edits. Generated mappings only; DCS defaults/execution are unverified." +
                    (diagnostics.Count == 0 ? "" : "\nPartial test — " + string.Join("; ", diagnostics.Distinct()));
                StartInputTestButton.IsEnabled = false;
                StopInputTestButton.IsEnabled = true;
                InputTestSnapshotChoice.IsEnabled = false;
                _timer.Start();
                Poll(this, EventArgs.Empty);
            }
            catch (Exception ex) { Stop("Input test could not start: " + ex.Message); }
        }

        private void StopInputTestClicked(object sender, RoutedEventArgs e) => Stop("Stopped. Start again to capture a fresh generated profile.");
        private void Stop(string status)
        {
            _timer.Stop();
            _session?.Reset(); _session = null; _plan = null;
            _testSnapshot = null;
            UpdateModifierLegend();
            RefreshLiveArtwork();
            _inputFeedbackDevice = null;
            _inputSource?.Dispose(); _inputSource = null;
            _testKeyboard.Clear(); _liveAxisUntil.Clear(); _livePressedInputs.Clear();
            ClearInputHighlights();
            if (StartInputTestButton is null) return;
            StartInputTestButton.IsEnabled = EditionAccess.Policy.IsOwner && _context?.Saved is not null;
            StopInputTestButton.IsEnabled = false;
            InputTestSnapshotChoice.IsEnabled = true;
            InputTestStatus.Text = status;
            InputTestModifiers.Text = "";
        }

        private void Poll(object? sender, EventArgs e)
        {
            if (_inputSource is null || _session is null || _plan is null) return;
            try
            {
                var frames = _inputSource.Read().ToList();
                frames.Add(new(Guid.Empty, _testKeyboard.ToDictionary(x => x.Key, x => Keyboard.IsKeyDown(x.Value) ? 1.0 : 0.0)));
                var physicalDevice = CurrentInputTestDevice();
                if (_inputFeedbackDevice != physicalDevice)
                {
                    InputTestLog.Items.Clear();
                    _events.Clear();
                    InputTestLogMessage.Text = "";
                    _liveAxisUntil.Clear();
                    _livePressedInputs.Clear();
                    _inputFeedbackDevice = physicalDevice;
                }
                var results = _session.ProcessDevice(frames, physicalDevice);
                foreach (var result in results)
                {
                    var commands = result.PhysicalGuid == Guid.Empty ? "Keyboard modifier input (keyboard commands are outside v1)" : result.Matches.Count == 0 ? "Unassigned in generated profiles" : string.Join(" | ", result.Matches.Select(x =>
                        $"{x.Layer} [{(x.Chord.Length == 0 ? "Base" : x.Chord)}] {x.Binding.CommandName} ({x.Binding.CommandId})" +
                        (x.Binding.Filter is null ? "" : " tuning=" + JsonSerializer.Serialize(x.Binding.Filter))));
                    var modifiers = string.Join(" / ", _session.ActiveModifiers.Select(x => x.Key + ":" + (x.Value.Length == 0 ? "Base" : x.Value)));
                    _events.Insert(0, new InputTestLogEntry(DateTime.Now, result.PhysicalGuid, result.InputKey,
                        result.Transition, result.Value, modifiers, commands));
                    while (_events.Count > 200) _events.RemoveAt(_events.Count - 1);
                    if (result.Transition == "Position") _liveAxisUntil[(result.PhysicalGuid, result.InputKey)] = DateTime.UtcNow.AddMilliseconds(500);
                    else if (result.Transition == "Press") _livePressedInputs.Add((result.PhysicalGuid, result.InputKey));
                    else if (result.Transition == "Release") _livePressedInputs.Remove((result.PhysicalGuid, result.InputKey));
                }
                InputTestModifiers.Text = string.Join(" · ", _session.ActiveModifiers.Select(x => x.Key + " modifiers: " + (x.Value.Length == 0 ? "Base" : x.Value)));
                UpdateModifierLegend();
                RefreshLiveArtwork();
                if (results.Count > 0) RefreshEventLog();
                HighlightInputControls();
                UpdateLogButtons();
            }
            catch (Exception ex) { Stop("Stopped on input loss/disconnect: " + ex.Message + ". Restart after reconnecting."); }
        }

        private void CopyInputTestLogClicked(object sender, RoutedEventArgs e)
        {
            var rows = InputTestLogFormatter.SelectRows(InputTestLog.Items.Cast<InputTestLogEntry>().ToArray(),
                InputTestLog.SelectedItems.Cast<InputTestLogEntry>().ToArray());
            if (rows.Count == 0) return;
            try
            {
                var content = InputTestLogFormatter.Format(rows);
                if (_context!.InputLogClipboardWriter is not null) _context!.InputLogClipboardWriter(content.Html, content.Text);
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

        private void ClearInputTestLogClicked(object sender, RoutedEventArgs e)
        {
            InputTestLog.Items.Clear();
            _events.Clear();
            InputTestLogMessage.Text = "Log cleared.";
            UpdateLogButtons();
        }
        private void RenderDiagram()
        {
            Diagram.Children.Clear();
            _callouts.Clear();
            _highlightColors.Clear();
            _currentDiagram = null;
            _currentDiagramModel = null;
            _currentArtwork = null;
            _lastModifierSignature = null;
            _legendItems = [];
            ModifierLegend.ItemsSource = _legendItems;
            var snapshot = _testSnapshot ?? _context?.Working;
            if (snapshot is null || FeedbackDevice.SelectedItem is not WorkspaceDevice device) return;

            var diagram = snapshot.Diagrams.FirstOrDefault(x => x.ProfileId == device.ProfileId);
            if (diagram is null || string.IsNullOrWhiteSpace(diagram.Svg)) return;

            DeviceTitle.Text = diagram.Title;
            var model = InputTestDiagramComposer.Compose(
                diagram,
                snapshot,
                _testSnapshot is not null ? snapshot.Assignments : _context?.Draft?.Snapshot ?? snapshot.Assignments,
                _testSnapshot is not null ? snapshot.Labels : _context?.Labels ?? snapshot.Labels,
                _session?.ActiveModifiers, AppearanceManager.ArtworkOpacityFor(this));
            _legendItems = model.Legend.Select(item => new InputTestLegendItem(item)).ToArray();
            ModifierLegend.ItemsSource = _legendItems;
            UpdateModifierLegend();

            using var stream = new MemoryStream(model.BackgroundPng);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            Diagram.Width = model.Width;
            Diagram.Height = model.Height;
            _currentDiagram = diagram;
            _currentDiagramModel = model;
            _currentArtwork = new Image { Source = bitmap, Width = model.Width, Height = model.Height };
            _lastModifierSignature = ModifierSignature();
            Diagram.Children.Add(_currentArtwork);

            foreach (var region in model.Callouts)
            {
                // Only the outline lights up; the finished kneeboard text remains unobscured.
                var border = new ContrastOutlineBorder
                {
                    Width = region.Width,
                    Height = region.Height,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(3),
                    Background = Brushes.Transparent,
                    Tag = region.InputKey + "|" + region.ControlId,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(border, region.X);
                Canvas.SetTop(border, region.Y);
                Diagram.Children.Add(border);
                _callouts.Add((region.InputKey, region.ControlId, border));
            }
        }

        private string ModifierSignature()
        {
            var active = _session?.ActiveModifiers;
            return (active?.GetValueOrDefault(AssignmentLayer.Module) ?? "") + "|" +
                   (active?.GetValueOrDefault(AssignmentLayer.UiLayer) ?? "");
        }

        private void RefreshLiveArtwork()
        {
            if (_currentDiagram is null || _currentDiagramModel is null || _currentArtwork is null) return;
            var signature = ModifierSignature();
            if (signature == _lastModifierSignature) return;
            var active = _session?.ActiveModifiers ?? new Dictionary<AssignmentLayer, string>();
            var png = InputTestDiagramComposer.RenderLive(_currentDiagram, _currentDiagramModel.Page, active, AppearanceManager.ArtworkOpacityFor(this));
            using var stream = new MemoryStream(png);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            _currentArtwork.Source = bitmap;
            _lastModifierSignature = signature;
        }

        private void UpdateModifierLegend()
        {
            foreach (var item in _legendItems)
                item.IsActive = _session is not null &&
                    KneeboardLegendActivity.IsActive(item.Entry, _session.ActiveModifiers);
        }

        private void FitDiagramClicked(object sender, RoutedEventArgs e) => _viewport.Fit();

        private void Zoom_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Zoom.Value = 1;
        }

        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZoomPercent is null) return;
            ZoomPercent.Text = ((int)(Zoom.Value * 100)).ToString(CultureInfo.InvariantCulture);
            if (Diagram is not null)
                Diagram.LayoutTransform = new ScaleTransform(Zoom.Value, Zoom.Value);
        }

        private void ZoomPercentChanged(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ZoomPercent.Text, out var percent))
                Zoom.Value = Math.Clamp(percent / 100.0, Zoom.Minimum, Zoom.Maximum);
        }

        private void EventFilterChanged(object sender, RoutedEventArgs e) => RefreshEventLog();

        private void RefreshEventLog()
        {
            if (InputTestLog is null || EventFilter is null || ShowAxis is null) return;
            var filter = (EventFilter.SelectedItem as ComboBoxItem)?.Content as string ?? "Both";
            InputTestLog.Items.Clear();
            foreach (var entry in _events.Where(x => x.Transition == "Position" ? ShowAxis.IsChecked == true
                : filter == "Both" || x.Transition == filter)) InputTestLog.Items.Add(entry);
            UpdateLogButtons();
        }

        private void UpdateLogButtons()
        {
            ClearInputTestLogButton.IsEnabled = InputTestLog.HasItems;
            CopyInputTestLogButton.IsEnabled = InputTestLog.HasItems;
        }

        private Guid? CurrentInputTestDevice() => FeedbackDevice.SelectedItem is WorkspaceDevice device
            ? DcsPhysicalIdentity.FromFileName(device.FileName) : null;

        private void ClearInputHighlights()
        {
            foreach (var border in _highlightColors.Keys)
            {
                border.BorderBrush = Brushes.Transparent;
                border.Effect = null;
            }
            _highlightColors.Clear();
        }

        private void HighlightInputControls()
        {
            var physical = CurrentInputTestDevice();
            var active = _session?.ActiveModifiers ?? new Dictionary<AssignmentLayer, string>();
            var desired = new Dictionary<Border, string>();
            foreach (var (input, control, border) in _callouts)
            {
                if (physical is not { } guid || _currentDiagramModel is null) continue;
                var pressed = _livePressedInputs.Any(x => x.Device == guid &&
                    DcsInputKey.SamePhysicalInput(x.Key, input));
                var moving = _liveAxisUntil.Any(x => x.Key.Device == guid &&
                    DcsInputKey.SamePhysicalInput(x.Key.Key, input) && x.Value > DateTime.UtcNow);
                if (!pressed && !moving) continue;
                var color = KneeboardLiveProjection.ActiveColor(_currentDiagramModel.Page, control,
                    _currentDiagram?.Controls ?? [], active);
                if (color is not null) desired[border] = color;
            }
            foreach (var (border, color) in _highlightColors)
                if (!desired.TryGetValue(border, out var next) || next != color)
                {
                    border.BorderBrush = Brushes.Transparent;
                    border.Effect = null;
                }
            foreach (var (border, color) in desired)
                if (!_highlightColors.TryGetValue(border, out var old) || old != color)
                {
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                }
            _highlightColors.Clear();
            foreach (var (border, color) in desired) _highlightColors.Add(border, color);
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
        #endregion END private helpers
    }
}
