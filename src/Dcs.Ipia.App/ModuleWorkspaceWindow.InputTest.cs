using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    public partial class ModuleWorkspaceWindow
    {
        // Input test pane methods extracted from the main code-behind (issue #202 slice 1).
        // Fields remain in the primary partial for now; further extraction to a dedicated
        // UserControl + ViewModel is planned in subsequent slices.

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
    }
}
