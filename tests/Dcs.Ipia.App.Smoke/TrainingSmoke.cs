using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using DcsIpia.Features.InputTest;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Runs both shipped training sequences through their real controls and verifies isolation and playback.</summary>
    public static class TrainingSmoke
    {
        private static readonly string[] AxisKeys = ["JOY_X", "JOY_Y"];
        #region BEGIN Public
        /// <summary>Checks scenario completion, independent intermediate state, restart, failure recovery and cleanup.</summary>
        public static void Verify()
        {
            var existingWindows = System.Windows.Application.Current.Windows.Cast<Window>().ToHashSet();
            var window = new TrainingWindow();
            window.Show();
            PumpUntil(() => window.Runner is not null, "Training did not initialize.");
            var scenarios = (ComboBox)window.FindName("ScenarioChoice");
            var next = (Button)window.FindName("NextButton");
            var restart = (Button)window.FindName("RestartButton");
            var oldDirectory = window.TrainingDirectory!;
            Click((Button)window.FindName("PlayButton"));
            PumpUntil(() => window.Runner!.State != TrainingState.Playing, "First checkpoint did not pause.");
            if (window.Runner!.CompletedSteps != 1 || window.Runner.State != TrainingState.Paused ||
                string.IsNullOrWhiteSpace(((TextBlock)window.FindName("ExplanationText")).Text))
                throw new InvalidOperationException("Checkpoint lost its explanation.");
            Capture(window, "training-new-module");
            window.Width = 1400;
            window.UpdateLayout();
            foreach (var theme in new[] { "Dark", "Light" })
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
                { Source = new Uri("pack://application:,,,/Dcs.Ipia.App;component/Themes/" + theme + ".xaml", UriKind.Absolute) };
                window.UpdateLayout();
                Capture(window, "training-new-module-" + theme);
                if (((AdornerDecorator)((Border)((Grid)window.Content).Children[0]).Child).AdornerLayer is null)
                    throw new InvalidOperationException("Training lost its highlighting layer.");
            }
            // A missing fixture must fail at its action and offer restart, without silently skipping.
            File.Delete(Path.Combine(oldDirectory, "commands.json"));
            while (window.Runner.CompletedSteps < 6 && window.Runner.State != TrainingState.Failed)
            {
                Click(next);
                PumpUntil(() => !window.Runner.IsBusy, "Training step did not settle.");
            }
            if (window.Runner.State != TrainingState.Failed || !window.Runner.Error!.Contains("catalog", StringComparison.Ordinal))
                throw new InvalidOperationException("Missing fixture did not stop training with step context.");
            Click(restart);
            PumpUntil(() => window.Runner.State == TrainingState.Ready, "Training restart failed.");
            if (window.TrainingDirectory == oldDirectory || Directory.Exists(oldDirectory))
                throw new InvalidOperationException("Restart retained its old training environment.");
            for (var scenario = 0; scenario < scenarios.Items.Count; scenario++)
            {
                // Exercise both supported narrow-window sizes rather than only the default 1620px window.
                window.Width = scenario == 0 ? 1280 : 1400;
                window.UpdateLayout();
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try { scenarios.SelectedIndex = scenario; }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                PumpUntil(() => next.IsEnabled, "Scenario selection did not initialize.");
                var root = window.TrainingDirectory!;
                var runner = window.Runner!;
                var scenarioData = (TrainingScenario)scenarios.SelectedItem;
                foreach (var step in scenarioData.Steps)
                {
                    Click(next);
                    // Rapid duplicate events cannot start a second action while the current one owns the runner.
                    if (runner.IsBusy) runner.NextAsync().GetAwaiter().GetResult();
                    PumpUntil(() => !runner.IsBusy, "Training step timed out: " + step.Id);
                    if (runner.State == TrainingState.Failed)
                        throw new InvalidOperationException(runner.Error);
                    CheckTrainingState(window, step.Action);
                    if (step.Action == "drag" && !window.AreDragEndpointsVisible)
                        throw new InvalidOperationException("Training drag endpoints were clipped: " + step.Id + "; " + window.HighlightedTargetVisibilityDetails);
                    if (step.Action is "base" or "kneeboard" or "drag-ready" or "drag" or "axis-options" or
                        "axis" or "switches" or
                        "test-base" or "test-hold" or "test-toggle-release" or "test-switches" or "test-axes")
                    {
                        Capture(window, "training-" + scenarioData.Id + "-" + step.Action);
                        // Full containment only for compact targets (diagram callouts / previews).
                        // Large controls such as the input-test log cannot fit entirely in the viewport.
                        if (step.Action is "base" or "kneeboard" or "axis" or "switches" && !window.IsHighlightedTargetVisible)
                            throw new InvalidOperationException("Training target is outside the visible viewport: " + step.Id + "; " + window.HighlightedTargetVisibilityDetails);
                    }
                    if (runner.CurrentStep?.Id != step.Id)
                        throw new InvalidOperationException("Training skipped or repeated a step: " + step.Id);
                }
                if (runner.State != TrainingState.Completed || !File.Exists(Path.Combine(root, "Outputs", "training.ipia-module.json")) ||
                    Directory.GetFiles(Path.Combine(root, "Outputs"), "*.zip").Length == 0)
                    throw new InvalidOperationException("Training did not generate its real snapshot/package outputs.");
                var profile = Directory.GetFiles(root, "*.diff.lua", SearchOption.AllDirectories).Single();
                if (File.ReadAllText(profile) != TrainingAssets.Read("profile.lua"))
                    throw new InvalidOperationException("Training changed imported source data.");
                Click(restart);
                PumpUntil(() => window.Runner!.State == TrainingState.Ready, "Completed scenario did not restart.");
                CheckRetainedWindows(existingWindows, window);
                if (Directory.Exists(root)) throw new InvalidOperationException("Restart retained completed scenario data.");
            }
            var lastDirectory = window.TrainingDirectory!;
            window.Close();
            PumpUntil(() => !window.IsVisible, "Exit training did not close.");
            if (Directory.Exists(lastDirectory)) throw new InvalidOperationException("Training did not clean its data.");
            CheckRetainedWindows(existingWindows);
            VerifyAxisEditorExit(existingWindows);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void CheckRetainedWindows(HashSet<Window> existingWindows, Window? training = null)
        {
            var retained = System.Windows.Application.Current.Windows.Cast<Window>()
                .Where(window => !existingWindows.Contains(window) && !ReferenceEquals(window, training)).ToArray();
            if (retained.Length != 0)
                throw new InvalidOperationException("Training retained hidden WPF windows that prevent application shutdown: " +
                    string.Join(", ", retained.Select(window => window.GetType().Name)));
            if (existingWindows.Any(window => !System.Windows.Application.Current.Windows.Cast<Window>().Contains(window)))
                throw new InvalidOperationException("Training cleanup closed an unrelated application window.");
        }
        private static void VerifyAxisEditorExit(HashSet<Window> existingWindows)
        {
            // The axis editor replaces the creation form reference while a dirty workspace is still mounted.
            // Exiting at this checkpoint must close all three unshown host windows without a discard prompt.
            var window = new TrainingWindow();
            window.Show();
            PumpUntil(() => window.Runner is not null, "Early-exit training did not initialize.");
            var next = (Button)window.FindName("NextButton");
            var scenario = (TrainingScenario)((ComboBox)window.FindName("ScenarioChoice")).SelectedItem;
            foreach (var step in scenario.Steps)
            {
                Click(next);
                PumpUntil(() => !window.Runner!.IsBusy, "Early-exit step did not settle: " + step.Id);
                if (window.Runner!.State == TrainingState.Failed) throw new InvalidOperationException(window.Runner.Error);
                if (step.Action == "axis-options") break;
            }
            if (window.Runner!.CurrentStep?.Action != "axis-options")
                throw new InvalidOperationException("Early-exit smoke did not reach the axis editor.");
            var directory = window.TrainingDirectory!;
            window.Close();
            PumpUntil(() => !window.IsVisible, "Training exit from the axis editor did not finish.");
            CheckRetainedWindows(existingWindows);
            if (Directory.Exists(directory)) throw new InvalidOperationException("Early exit retained training data.");
        }
        private static void CheckTrainingState(TrainingWindow window, string action)
        {
            var root = (FrameworkElement)((ContentControl)window.FindName("ApplicationView")).Content;
            if (action.StartsWith("context-", StringComparison.Ordinal))
            {
                var feature = (FrameworkElement)((ContentControl)root.FindName("FeatureContent")).Content;
                var menu = Descendants((Canvas)feature.FindName("Diagram")).OfType<Border>()
                    .Select(x => x.ContextMenu).Single(x => x?.IsOpen == true)!;
                if (!menu.Items.OfType<MenuItem>().Any(x => Equals(x.Header, "Change label\u2026")) ||
                    !menu.Items.OfType<MenuItem>().Any(x => Equals(x.Header, "Clear assignment")) ||
                    action == "context-axis" && !menu.Items.OfType<MenuItem>().Any(x => Equals(x.Header, "Tune axis\u2026")))
                    throw new InvalidOperationException("Training context menu is incomplete: " + action);
                Capture(menu, "training-" + ((TrainingScenario)((ComboBox)window.FindName("ScenarioChoice")).SelectedItem).Id + "-" + action);
            }
            if (action == "axis-options")
            {
                if (((TextBox)root.FindName("Deadzone")).Text != "3" ||
                    ((TextBox)root.FindName("Curvature")).Text != "15")
                    throw new InvalidOperationException("Axis options are not visible at the reading checkpoint.");
                return;
            }
            if (action == "toggle")
            {
                var feature = (FrameworkElement)((ContentControl)root.FindName("FeatureContent")).Content;
                var modifiers = ((DataGrid)feature.FindName("ManagedModifiers")).Items.Cast<WorkspaceModifier>().ToArray();
                if (!modifiers.Any(x => x.Name == "TRAIN_SHIFT" && x.Mode == ModifierMode.Hold) ||
                    !modifiers.Any(x => x.Name == "TRAIN_TOGGLE" && x.Mode == ModifierMode.Toggle))
                    throw new InvalidOperationException("Training did not create both modifier modes.");
            }
            if (!action.StartsWith("test-", StringComparison.Ordinal)) return;
            var view = (InputTestView)((ContentControl)root.FindName("FeatureContent")).Content;
            var log = ((ListBox)view.FindName("InputTestLog")).Items.Cast<InputTestLogEntry>().ToArray();
            var legend = ((ItemsControl)view.FindName("ModifierLegend")).Items.Cast<InputTestLegendItem>().ToArray();
            bool Active(string chord) => legend.Any(x => x.Entry.Layer == AssignmentLayer.Module && x.Entry.Chord == chord && x.IsActive);
            var valid = action switch
            {
                "test-start" => log.Length == 0,
                "test-base" => log.Any(x => x.InputKey == "JOY_BTN1" && x.Commands.Contains("Sample landing light", StringComparison.Ordinal)),
                "test-hold" => Active("TRAIN_SHIFT"),
                "test-release" => !Active("TRAIN_SHIFT"),
                "test-toggle-release" => Active("TRAIN_TOGGLE") && log.Any(x => x.Commands.Contains("Sample toggle-layer action", StringComparison.Ordinal)),
                "test-toggle-off" => !Active("TRAIN_TOGGLE"),
                "test-switches" => Enumerable.Range(6, 5).All(index => log.Any(x => x.InputKey == "JOY_BTN" + index && x.Transition == "Press" && x.Commands.Contains("Sample", StringComparison.Ordinal))),
                "test-axes" => AxisKeys.All(key => log.Any(x => x.InputKey == key && x.Transition == "Position")) &&
                    log.Any(x => x.InputKey == "JOY_Y" && x.Commands.Contains("tuning=", StringComparison.Ordinal)),
                "test-stop" => !legend.Any(x => x.IsActive) && ((Button)view.FindName("StartInputTestButton")).IsEnabled,
                _ => true
            };
            if (!valid) throw new InvalidOperationException("Training input-test acceptance failed: " + action);
        }
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        private static void Capture(FrameworkElement window, string name)
        {
            var folder = Environment.GetEnvironmentVariable("IPIA_MAPPING_PREVIEW");
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(stream);
        }
        private static void Click(Button button)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void PumpUntil(Func<bool> done, string message)
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!done())
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException(message);
                System.Windows.Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
