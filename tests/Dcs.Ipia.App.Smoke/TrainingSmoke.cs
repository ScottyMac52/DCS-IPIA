using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Runs both shipped training sequences through their real controls and verifies isolation and playback.</summary>
    public static class TrainingSmoke
    {
        #region BEGIN Public
        /// <summary>Checks scenario completion, independent intermediate state, restart, failure recovery and cleanup.</summary>
        public static void Verify()
        {
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
                    if (step.Action is "base" or "kneeboard")
                    {
                        Capture(window, "training-" + scenarioData.Id + "-" + step.Action);
                        if (!window.IsHighlightedTargetVisible)
                            throw new InvalidOperationException("Training target is outside the visible viewport: " + step.Id);
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
            }
            var lastDirectory = window.TrainingDirectory!;
            window.Close();
            PumpUntil(() => !window.IsVisible, "Exit training did not close.");
            if (Directory.Exists(lastDirectory)) throw new InvalidOperationException("Training did not clean its data.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Capture(Window window, string name)
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
