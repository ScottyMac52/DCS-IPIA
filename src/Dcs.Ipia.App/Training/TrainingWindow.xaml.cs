using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Hosts actual application content with independent playback controls and a disposable training environment.</summary>
    public partial class TrainingWindow : Window, IAsyncDisposable
    {
        private TrainingEnvironment? _environment;
        private TrainingActions? _actions;
        private TrainingRunner? _runner;
        private TrainingHighlightAdorner? _highlight;
        private AdornerLayer? _highlightLayer;
        private bool _changing;
        private bool _closing;
        private bool _closed;
        private bool _exitRequested;

        /// <summary>Offers both bundled workflows without opening user data or hardware.</summary>
        public TrainingWindow()
        {
            InitializeComponent();
            Closing += ClosingTraining;
            Loaded += (_, _) => ScenarioChoice.SelectedIndex = 0;
            try { ScenarioChoice.ItemsSource = TrainingAssets.Scenarios(); }
            catch (Exception ex) { ErrorText.Text = "Training assets could not load: " + ex.Message; }
            UpdateControls();
        }
        #region BEGIN Public
        /// <summary>Releases the isolated training session after active actions settle.</summary>
        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
            GC.SuppressFinalize(this);
        }
        /// <summary>Gets the active runner for independent Windows acceptance checks.</summary>
        public TrainingRunner? Runner => _runner;
        /// <summary>Gets the isolated folder for acceptance checks and diagnostics while training is open.</summary>
        public string? TrainingDirectory => _environment?.Root;
        #endregion END Public
        #region BEGIN private helpers
        private async void ScenarioChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_changing || ScenarioChoice.SelectedItem is not TrainingScenario scenario) return;
            _changing = true;
            UpdateControls();
            try
            {
                await ReleaseAsync();
                await CreateEnvironmentAsync();
                _runner = new TrainingRunner(scenario, step => _actions!.ExecuteAsync(step));
                _runner.Changed += RunnerChanged;
                SpeedChanged(this, null!);
                StepTitle.Text = scenario.Title;
                ExplanationText.Text = "Choose Play to watch the actual application, or Next step to advance at your own pace. All sample files and outputs are isolated and removed when you exit.";
                ErrorText.Text = "";
            }
            catch (Exception ex) { ErrorText.Text = "Unable to prepare training: " + ex.Message + " Choose another scenario or exit."; }
            finally { _changing = false; UpdateControls(); if (_exitRequested) Close(); }
        }
        private async Task CreateEnvironmentAsync()
        {
            _environment = new TrainingEnvironment();
            await _environment.InitializeAsync();
            _actions = new TrainingActions(_environment, Mount, Highlight);
        }
        private void Mount(Window window)
        {
            ClearHighlight();
            var content = (FrameworkElement)window.Content;
            window.Content = null;
            content.DataContext = window.DataContext;
            ApplicationView.Content = content;
            ApplicationView.UpdateLayout();
        }
        private void Highlight(FrameworkElement target)
        {
            ClearHighlight();
            _highlightLayer = AdornerLayer.GetAdornerLayer(target)
                ?? throw new InvalidOperationException("Training highlight layer is unavailable.");
            _highlight = new TrainingHighlightAdorner(target);
            _highlightLayer.Add(_highlight);
        }
        private void ClearHighlight()
        {
            if (_highlight is not null) _highlightLayer?.Remove(_highlight);
            _highlight = null;
            _highlightLayer = null;
        }
        private void RunnerChanged(object? sender, EventArgs e)
        {
            if (_runner is null) return;
            if (_runner.CurrentStep is { } step)
            {
                StepTitle.Text = step.Title;
                ExplanationText.Text = step.Explanation;
            }
            ErrorText.Text = _runner.Error ?? "";
            UpdateControls();
        }
        private void UpdateControls()
        {
            if (PlayButton is null) return;
            var available = !_changing && !_closing && _runner is not null;
            var canAdvance = available && !_runner!.IsBusy && _runner.State is TrainingState.Ready or TrainingState.Paused;
            PlayButton.IsEnabled = NextButton.IsEnabled = canAdvance;
            PauseButton.IsEnabled = available && _runner!.State == TrainingState.Playing;
            RestartButton.IsEnabled = available && !_runner!.IsBusy;
            ScenarioChoice.IsEnabled = !_changing && !_closing && _runner?.IsBusy != true;
            ExitButton.IsEnabled = !_closing;
            ProgressText.Text = _runner is null ? "Preparing training…" :
                $"{_runner.State} · {_runner.CompletedSteps} / {((TrainingScenario)ScenarioChoice.SelectedItem).Steps.Count} steps complete";
        }
        private async void PlayClicked(object sender, RoutedEventArgs e)
        {
            if (_runner is not null) await _runner.PlayAsync();
        }
        private void PauseClicked(object sender, RoutedEventArgs e) => _runner?.Pause();
        private async void NextClicked(object sender, RoutedEventArgs e)
        {
            if (_runner is not null) await _runner.NextAsync();
        }
        private async void RestartClicked(object sender, RoutedEventArgs e)
        {
            if (_runner is null || _changing || _runner.IsBusy) return;
            _changing = true;
            UpdateControls();
            try
            {
                await _runner.RestartAsync(async () =>
                {
                    ReleaseViews();
                    _environment?.Dispose();
                    await CreateEnvironmentAsync();
                });
                StepTitle.Text = "Ready to restart";
                ExplanationText.Text = "The sample database and files have been recreated. Choose Play or Next step.";
                ErrorText.Text = _runner.Error ?? "";
            }
            finally { _changing = false; UpdateControls(); if (_exitRequested) Close(); }
        }
        private void SpeedChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_runner is not null && SpeedChoice.SelectedItem is ComboBoxItem { Tag: string speed })
                _runner.Speed = double.Parse(speed, CultureInfo.InvariantCulture);
        }
        private void ExitClicked(object sender, RoutedEventArgs e) => Close();
        private void BlockApplicationKeys(object sender, KeyEventArgs e) => e.Handled = true;
        private async void ClosingTraining(object? sender, CancelEventArgs e)
        {
            if (_closed) return;
            e.Cancel = true;
            if (_changing) { _exitRequested = true; return; }
            if (_closing) return;
            _closing = true;
            UpdateControls();
            try
            {
                await ReleaseAsync();
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                _closed = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Training cleanup failed: " + ex.Message + " Try Exit again.";
                _closing = false;
                UpdateControls();
            }
        }
        private async Task ReleaseAsync()
        {
            if (_runner is not null)
            {
                _runner.Changed -= RunnerChanged;
                await _runner.DisposeAsync();
                _runner = null;
            }
            ReleaseViews();
            _environment?.Dispose();
            _environment = null;
        }
        private void ReleaseViews()
        {
            ClearHighlight();
            ApplicationView.Content = null;
            _actions?.Close();
            _actions = null;
        }
        #endregion END private helpers
    }
}
