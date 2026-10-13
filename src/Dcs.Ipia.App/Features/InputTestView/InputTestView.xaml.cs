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


    public partial class InputTestView : UserControl, IDisposable
    {
        private IModuleWorkspaceContext? _context;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private IDeviceInputSource? _inputSource;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        private InputTestSession? _session;
        private GeneratedInputTestPlan? _plan;
        private readonly Dictionary<string, Border> _callouts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _flashUntil = new(StringComparer.Ordinal);
        private readonly List<string> _allEvents = [];
        private Button? StartButton => ActionBar.FindButton(this, "Start test");
        private Button? StopButton => ActionBar.FindButton(this, "Stop test");

        private static readonly Brush[] LayerColors =
        [
            Brushes.Gray,            // 0 = Base
            Brushes.DodgerBlue,      // 1
            Brushes.Orange,          // 2
            Brushes.MediumPurple,    // 3
            Brushes.Gold,            // 4
            Brushes.LimeGreen,       // 5
            Brushes.HotPink,         // 6
            Brushes.Cyan,            // 7
            Brushes.Coral,           // 8
            Brushes.MediumSeaGreen,  // 9
            Brushes.SteelBlue,       // 10
            Brushes.Orchid,          // 11
            Brushes.Khaki,           // 12
        ];

        public InputTestView()
        {
            InitializeComponent();
            _timer.Tick += Poll;
            Unloaded += (_, _) => Stop("Stopped.");
        }

        #region IDisposable Support

        public void Dispose()
        {
            Stop("Stopped.");
            _timer.Tick -= Poll;
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support

        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            StatusText.Text = context.Working is null
                ? "No working snapshot"
                : $"Input test · {context.Working.Configuration.Module.DisplayName}";
            StartButton?.IsEnabled = context.Working is not null;

            if (context.Working is not null)
            {
                var devices = WorkspaceStructureEditor.Describe(context.Working)
                    .Devices.Where(x => x.Layer == AssignmentLayer.Module).ToArray();
                FeedbackDevice.ItemsSource = devices;
                FeedbackDevice.SelectedIndex = 0;
                FeedbackDevice.SelectionChanged += (_, _) => RenderDiagram();
            }

            RenderDiagram();
        }

        private void StartInputTestClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;
            Stop("Preparing…");
            try
            {
                var plan = GeneratedInputTestPlan.Create(_context.Working);
                var required = plan.Bindings.Select(x => x.PhysicalGuid)
                    .Concat(plan.Modifiers.Select(x => x.PhysicalGuid))
                    .Where(x => x != Guid.Empty)
                    .Distinct()
                    .ToArray();

                var attached = _context.DeviceDiscovery.Discover()
                    .Where(x => x.Connected)
                    .Select(x => x.PhysicalGuid)
                    .ToHashSet();
                var devices = required.Where(attached.Contains).ToArray();

                var window = Window.GetWindow(this) ?? throw new InvalidOperationException("Input test must be hosted in a window.");
                var handle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                _inputSource = new WindowsDeviceInputSource(handle, devices);

                InputTestStatus.Text = $"Testing at {DateTime.Now:T} · {devices.Length} of {required.Length} controllers connected."; _plan = plan;
                _session = new InputTestSession(plan.Bindings, plan.Modifiers);

                InputTestStatus.Text = $"Testing at {DateTime.Now:T} · {required.Length} controllers.";
                StartButton?.IsEnabled = false;
                StopButton?.IsEnabled = true;
                _timer.Start();
            }
            catch (Exception ex)
            {
                Stop("Could not start: " + ex.Message);
            }
        }

        private void StopInputTestClicked(object sender, RoutedEventArgs e) => Stop("Stopped.");

        private void Stop(string status)
        {
            _timer.Stop();
            _session?.Reset();
            _session = null;
            _plan = null;
            _inputSource?.Dispose();
            _inputSource = null;
            if (StartButton is null) return;
            StartButton?.IsEnabled = _context?.Working is not null;
            StopButton?.IsEnabled = false;
            InputTestStatus.Text = status;
            InputTestModifiers.Text = "";
        }

        private void Poll(object? sender, EventArgs e)
        {
            if (_inputSource is null || _session is null || _plan is null) return;
            try
            {
                var frames = _inputSource.Read().ToList();

                Guid? feedback = null;
                if (FeedbackDevice.SelectedItem is WorkspaceDevice device)
                    feedback = DcsPhysicalIdentity.FromFileName(device.FileName);

                var results = _session.ProcessDevice(frames, feedback);
                var mapped = results.Where(r => r.Matches.Count > 0).ToList();

                foreach (var result in mapped)
                {
                    var isAxis = result.Transition == "Position";
                    var commands = string.Join(" | ", result.Matches.Select(x => x.Binding.CommandName));
                    var value = isAxis ? $"  {result.Value:0.00}" : "";
                    var line = $"{DateTime.Now:T}  {result.InputKey}  {result.Transition}{value}  {commands}";
                    _allEvents.Insert(0, line);
                    while (_allEvents.Count > 500) _allEvents.RemoveAt(_allEvents.Count - 1);
                }
                RefreshEventLog();

                foreach (var (key, border) in _callouts)
                {
                    var parts = key.Split('|');
                    if (parts.Length < 3) continue;
                    if (!Enum.TryParse<AssignmentLayer>(parts[1], out var layer)) continue;
                    var chord = parts[2];
                    var activeForLayer = _session.ActiveModifiers.GetValueOrDefault(layer) ?? "";
                    var isActive = chord == activeForLayer;
                    border.Opacity = isActive ? 1.0 : 0.5;
                    border.BorderThickness = isActive ? new Thickness(2) : new Thickness(0.5);
                }

                foreach (var result in results)
                {
                    if (result.Transition == "Release") continue;
                    var flashed = false;
                    foreach (var match in result.Matches)
                    {
                        var chord = _session.ActiveModifiers.GetValueOrDefault(match.Layer) ?? "";
                        var flashKey = $"{result.InputKey}|{match.Layer}|{chord}";
                        if (_callouts.TryGetValue(flashKey, out var border))
                        {
                            border.Background = Brushes.White;
                            _flashUntil[flashKey] = DateTime.UtcNow.AddMilliseconds(250);
                            flashed = true;
                        }
                    }
                    if (!flashed)
                    {
                        var flashKey = $"{result.InputKey}|{AssignmentLayer.Module}|";
                        if (_callouts.TryGetValue(flashKey, out var border))
                        {
                            border.Background = Brushes.LightCoral;
                            _flashUntil[flashKey] = DateTime.UtcNow.AddMilliseconds(250);
                        }
                    }
                }

                foreach (var (key, until) in _flashUntil.ToArray())
                {
                    if (until > DateTime.UtcNow) continue;
                    if (_callouts.TryGetValue(key, out var border))
                    {
                        var parts = key.Split('|');
                        var idx = parts.Length > 1 && parts[1] == nameof(AssignmentLayer.UiLayer) ? 1 : 0;
                        if (parts.Length > 2 && parts[2].Length > 0) idx += 1;
                        border.Background = LayerColors[idx % LayerColors.Length];
                    }
                    _flashUntil.Remove(key);
                }

                var summary = string.Join(" · ",
                    _session.ActiveModifiers.Select(x => x.Key + ": " + (x.Value.Length == 0 ? "Base" : x.Value)));
                InputTestStatus.Text = $"frames={frames.Count}  mapped={mapped.Count}";
                InputTestModifiers.Text = summary;
            }
            catch (Exception ex)
            {
                Stop("Stopped on input loss: " + ex.Message);
            }
        }

        private void ClearLogClicked(object sender, RoutedEventArgs e)
        {
            _allEvents.Clear();
            RefreshEventLog();
        }
        private void RenderDiagram()
        {
            Diagram.Children.Clear();
            _callouts.Clear();
            if (_context?.Working is null || FeedbackDevice.SelectedItem is not WorkspaceDevice device) return;

            var diagram = _context.Working.Diagrams.FirstOrDefault(x => x.ProfileId == device.ProfileId);
            if (diagram is null || string.IsNullOrWhiteSpace(diagram.Svg)) return;

            DeviceTitle.Text = diagram.Title;
            var model = InputTestDiagramComposer.Compose(
                diagram,
                _context.Working,
                _context.Draft?.Snapshot ?? _context.Working.Assignments,
                _context.Labels);

            using var stream = new MemoryStream(model.BackgroundPng);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            Diagram.Width = model.Width;
            Diagram.Height = model.Height;
            Diagram.Children.Add(new Image { Source = bitmap, Width = model.Width, Height = model.Height });

            foreach (var c in model.Callouts)
            {
                var border = new Border
                {
                    Width = c.Width,
                    Height = c.Height,
                    Background = LayerColors[c.ColorIndex % LayerColors.Length],
                    Opacity = 0.9,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0.5),
                    Child = new TextBlock
                    {
                        Text = c.Caption,
                        FontSize = 9,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = Brushes.Black,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Canvas.SetLeft(border, c.X);
                Canvas.SetTop(border, c.Y);
                Diagram.Children.Add(border);
                _callouts[$"{c.InputKey}|{c.Chord}"] = border;
            }
        }

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
            if (InputTestLog is null) return;
            var filter = (EventFilter.SelectedItem as ComboBoxItem)?.Content as string ?? "Both";
            var showAxis = ShowAxis.IsChecked == true;
            var filtered = _allEvents.Where(line =>
            {
                var isPress = line.Contains("  Press  ");
                var isRelease = line.Contains("  Release  ");
                var isAxis = line.Contains("  Position  ");
                if (isAxis && !showAxis) return false;
                return filter switch
                {
                    "Press" => isPress || isAxis,
                    "Release" => isRelease,
                    _ => true
                };
            }).Take(200);
            InputTestLog.ItemsSource = filtered.ToArray();
        }
    }
}