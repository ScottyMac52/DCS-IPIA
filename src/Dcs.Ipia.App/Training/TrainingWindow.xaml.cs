using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
        /// <summary>Gets whether the highlighted control is fully inside the visible application viewport.</summary>
        public bool IsHighlightedTargetVisible
        {
            get
            {
                if (_highlight?.AdornedElement is not FrameworkElement target || !ApplicationViewport.IsAncestorOf(target)) return false;
                ApplicationView.UpdateLayout();
                var bounds = target.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(target.RenderSize));
                var viewport = new Rect(0, 0, ApplicationViewport.ViewportWidth, ApplicationViewport.ViewportHeight);
                viewport.Inflate(2, 2);
                if (viewport.Contains(bounds)) return true;
                // Large targets (logs etc.) are acceptable if their top is in view and there is overlap.
                var intersection = Rect.Intersect(viewport, bounds);
                return !intersection.IsEmpty && bounds.Top >= -8 && bounds.Top < viewport.Height - 40;
            }
        }
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
            _actions = new TrainingActions(_environment, Mount, Highlight, AnimateDragAsync);
        }
        private void ViewportChanged(object sender, SizeChangedEventArgs e)
        {
            if (ApplicationView is null) return;
            // Prefer the actual viewport size so smaller windows don't force excessive outer scrolling.
            // Keep a modest floor so feature layouts don't collapse below usable dimensions.
            ApplicationView.Width = Math.Max(900, ApplicationViewport.ViewportWidth);
            ApplicationView.Height = Math.Max(700, ApplicationViewport.ViewportHeight);
        }
        private readonly Dictionary<Window, FrameworkElement> _mountedContent = [];
        private void Mount(Window window)
        {
            ClearHighlight();
            ApplicationView.Content = null;
            if (!_mountedContent.TryGetValue(window, out var content))
            {
                content = (FrameworkElement)window.Content;
                window.Content = null;
                _mountedContent.Add(window, content);
            }
            content.DataContext = window.DataContext;
            if (!content.Resources.MergedDictionaries.Contains(window.Resources))
                content.Resources.MergedDictionaries.Add(window.Resources);
            if (NameScope.GetNameScope(window) is { } scope) NameScope.SetNameScope(content, scope);
            ApplicationView.Content = content;
            ApplicationViewport.ScrollToTop();
            ApplicationViewport.ScrollToLeftEnd();
            ApplicationView.UpdateLayout();
        }
        private void Highlight(FrameworkElement target)
        {
            ClearHighlight();
            ApplicationView.UpdateLayout();

            // Scroll every nested ScrollViewer so the target is inside each of them first.
            ScrollTargetIntoAncestors(target);

            // Prefer centering a diagram callout in the outer viewport rather than always
            // pinning the diagram origin to the top-left (which hides lower items and the palette).
            if (target.Tag is HardwareGraphicControl)
            {
                CenterInViewport(target, preferUpper: false);
            }
            else
            {
                EnsureInViewport(target);
            }

            ApplicationView.UpdateLayout();
            _highlightLayer = AdornerLayer.GetAdornerLayer(target)
                ?? throw new InvalidOperationException("Training highlight layer is unavailable.");
            _highlight = new TrainingHighlightAdorner(target);
            _highlightLayer.Add(_highlight);
        }
        /// <summary>Scrolls every ancestor ScrollViewer (except the outer training viewport) so the target is inside it.</summary>
        private static void ScrollTargetIntoAncestors(FrameworkElement target)
        {
            DependencyObject? current = target;
            while (current is not null)
            {
                if (current is ScrollViewer viewer && viewer.Name != "ApplicationViewport")
                {
                    viewer.UpdateLayout();
                    var bounds = target.TransformToAncestor(viewer).TransformBounds(new Rect(target.RenderSize));
                    var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                    if (!viewport.Contains(bounds))
                    {
                        var vertical = bounds.Top < 0 ? bounds.Top : Math.Max(0, bounds.Bottom - viewport.Height);
                        var horizontal = bounds.Left < 0 ? bounds.Left : Math.Max(0, bounds.Right - viewport.Width);
                        viewer.ScrollToVerticalOffset(viewer.VerticalOffset + vertical);
                        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset + horizontal);
                        viewer.UpdateLayout();
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        private void EnsureInViewport(FrameworkElement target)
        {
            ApplicationView.UpdateLayout();
            var bounds = target.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(target.RenderSize));
            var vw = ApplicationViewport.ViewportWidth;
            var vh = ApplicationViewport.ViewportHeight;
            var width = Math.Min(bounds.Width, vw);
            var height = Math.Min(bounds.Height, vh);
            var horizontal = bounds.Left < 0 ? bounds.Left : Math.Max(0, bounds.Left + width - vw);
            var vertical = bounds.Top < 0 ? bounds.Top : Math.Max(0, bounds.Top + height - vh);
            ApplicationViewport.ScrollToHorizontalOffset(ApplicationViewport.HorizontalOffset + horizontal);
            ApplicationViewport.ScrollToVerticalOffset(ApplicationViewport.VerticalOffset + vertical);
        }
        private void CenterInViewport(FrameworkElement target, bool preferUpper)
        {
            ApplicationView.UpdateLayout();
            var bounds = target.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(target.RenderSize));
            var vw = ApplicationViewport.ViewportWidth;
            var vh = ApplicationViewport.ViewportHeight;
            // Center the callout, but bias slightly upward so the label above it stays readable.
            var targetCenterX = bounds.Left + bounds.Width / 2;
            var targetCenterY = bounds.Top + bounds.Height / 2;
            var desiredX = targetCenterX - vw / 2;
            var desiredY = preferUpper ? bounds.Top - 40 : targetCenterY - vh * 0.4;
            ApplicationViewport.ScrollToHorizontalOffset(ApplicationViewport.HorizontalOffset + desiredX);
            ApplicationViewport.ScrollToVerticalOffset(ApplicationViewport.VerticalOffset + desiredY);
            // Clamp so we don't overscroll past content.
            ApplicationViewport.ScrollToHorizontalOffset(Math.Max(0, ApplicationViewport.HorizontalOffset));
            ApplicationViewport.ScrollToVerticalOffset(Math.Max(0, ApplicationViewport.VerticalOffset));
        }
        private async Task AnimateDragAsync(FrameworkElement source, FrameworkElement destination)
        {
            // Make the palette item visible first so the gesture starts on-screen.
            ScrollTargetIntoAncestors(source);
            EnsureInViewport(source);
            ApplicationView.UpdateLayout();

            // Then bring the drop target into view (this may move the outer scroll).
            Highlight(destination);

            // If the source was pushed completely out of the viewport, compromise by
            // scrolling so both are at least partially visible when possible.
            ApplicationView.UpdateLayout();
            var srcBounds = source.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(source.RenderSize));
            var destBounds = destination.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(destination.RenderSize));
            var vw = ApplicationViewport.ViewportWidth;
            var vh = ApplicationViewport.ViewportHeight;
            if (srcBounds.Bottom < 0 || srcBounds.Top > vh || srcBounds.Right < 0 || srcBounds.Left > vw)
            {
                // Midpoint bias toward the destination.
                var midX = (srcBounds.Left + destBounds.Left) / 2;
                var midY = (srcBounds.Top + destBounds.Top) / 2;
                ApplicationViewport.ScrollToHorizontalOffset(ApplicationViewport.HorizontalOffset + midX - vw * 0.3);
                ApplicationViewport.ScrollToVerticalOffset(ApplicationViewport.VerticalOffset + midY - vh * 0.35);
                ApplicationViewport.ScrollToHorizontalOffset(Math.Max(0, ApplicationViewport.HorizontalOffset));
                ApplicationViewport.ScrollToVerticalOffset(Math.Max(0, ApplicationViewport.VerticalOffset));
                ApplicationView.UpdateLayout();
            }

            var layer = AdornerLayer.GetAdornerLayer(ApplicationView)!;
            var start = source.TranslatePoint(new Point(source.ActualWidth / 2, source.ActualHeight / 2), ApplicationView);
            var end = destination.TranslatePoint(new Point(destination.ActualWidth / 2, destination.ActualHeight / 2), ApplicationView);
            var drag = new TrainingDragAdorner(ApplicationView, start, end) { IsHitTestVisible = false };
            layer.Add(drag);
            try
            {
                for (var frame = 0; frame <= 24; frame++)
                {
                    drag.Progress = frame / 24.0;
                    drag.InvalidateVisual();
                    await Task.Delay(30);
                }
            }
            finally { layer.Remove(drag); }
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
            ProgressText.Text = _runner is null ? "Preparing training\u2026" :
                $"{_runner.State} \u00b7 {_runner.CompletedSteps} / {((TrainingScenario)ScenarioChoice.SelectedItem).Steps.Count} steps complete";
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
            _mountedContent.Clear();
            _actions?.Dispose();
            _actions = null;
        }
        #endregion END private helpers
    }
}
