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
                viewport.Inflate(4, 4);
                if (viewport.Contains(bounds)) return true;
                // Compact callouts must be fully contained. Only oversized reading panels may overlap.
                if (bounds.Width <= viewport.Width && bounds.Height <= viewport.Height) return false;
                var intersection = Rect.Intersect(viewport, bounds);
                return !intersection.IsEmpty && bounds.Top >= -12 && bounds.Top < viewport.Height - 20;
            }
        }
        /// <summary>Gets whether both endpoints were visible when the latest drag animation began.</summary>
        public bool AreDragEndpointsVisible { get; private set; }
        /// <summary>Describes the current target and scroll extent when a visibility acceptance check fails.</summary>
        public string HighlightedTargetVisibilityDetails => _highlight?.AdornedElement is FrameworkElement target
            ? $"target={target.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(target.RenderSize))}; " +
                $"viewport={ApplicationViewport.ViewportWidth}x{ApplicationViewport.ViewportHeight}; " +
                $"extent={ApplicationViewport.ExtentWidth}x{ApplicationViewport.ExtentHeight}; " +
                $"offset={ApplicationViewport.HorizontalOffset},{ApplicationViewport.VerticalOffset}"
            : "No highlighted target";
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
            // The real workspace has fixed-height headers, palette and inspector rows. Below this
            // native size the diagram overflows the content extent and cannot be reached by scrolling.
            ApplicationView.Width = Math.Max(1100, ApplicationViewport.ViewportWidth);
            ApplicationView.Height = Math.Max(1100, ApplicationViewport.ViewportHeight);
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

            // Bring the target into every nested ScrollViewer first (diagram viewers etc.).
            ScrollTargetIntoAncestors(target);

            // Then make sure it is fully inside the outer training viewport with minimal movement.
            EnsureFullyInOuterViewport(target);

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
                    // DataGrid/ListBox viewers use item offsets, not pixel offsets.
                    // Their owning control already scrolls the selected item into view.
                    if (viewer.CanContentScroll)
                    {
                        current = VisualTreeHelper.GetParent(current);
                        continue;
                    }
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
        /// <summary>Minimal outer-viewport scroll so the target bounds are fully contained when they fit.</summary>
        private void EnsureFullyInOuterViewport(FrameworkElement target)
        {
            ApplicationView.UpdateLayout();
            var bounds = target.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(target.RenderSize));
            var vw = ApplicationViewport.ViewportWidth;
            var vh = ApplicationViewport.ViewportHeight;

            double horizontal = 0;
            if (bounds.Left < 0)
                horizontal = bounds.Left;
            else if (bounds.Left + Math.Min(bounds.Width, vw) > vw)
                horizontal = bounds.Left + Math.Min(bounds.Width, vw) - vw;

            double vertical = 0;
            if (bounds.Top < 0)
                vertical = bounds.Top;
            else if (bounds.Top + Math.Min(bounds.Height, vh) > vh)
                vertical = bounds.Top + Math.Min(bounds.Height, vh) - vh;

            if (horizontal != 0)
                ApplicationViewport.ScrollToHorizontalOffset(ApplicationViewport.HorizontalOffset + horizontal);
            if (vertical != 0)
                ApplicationViewport.ScrollToVerticalOffset(ApplicationViewport.VerticalOffset + vertical);
        }
        private async Task AnimateDragAsync(FrameworkElement source, FrameworkElement destination)
        {
            // Make the palette source visible first.
            ScrollTargetIntoAncestors(source);
            EnsureFullyInOuterViewport(source);
            ApplicationView.UpdateLayout();

            // Highlight (and scroll) the drop target.
            Highlight(destination);

            // Scroll the union when it fits, so both ends stay visible. Scroll commands are queued;
            // do not overwrite a requested offset with the old VerticalOffset/HorizontalOffset.
            ApplicationView.UpdateLayout();
            var srcBounds = source.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(source.RenderSize));
            var destBounds = destination.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(destination.RenderSize));
            var combined = Rect.Union(srcBounds, destBounds);
            var vw = ApplicationViewport.ViewportWidth;
            var vh = ApplicationViewport.ViewportHeight;
            if (combined.Width <= vw)
            {
                var delta = combined.Left < 0 ? combined.Left : Math.Max(0, combined.Right - vw);
                ApplicationViewport.ScrollToHorizontalOffset(ApplicationViewport.HorizontalOffset + delta);
            }
            if (combined.Height <= vh)
            {
                var delta = combined.Top < 0 ? combined.Top : Math.Max(0, combined.Bottom - vh);
                ApplicationViewport.ScrollToVerticalOffset(ApplicationViewport.VerticalOffset + delta);
            }
            ApplicationViewport.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            var viewport = new Rect(0, 0, vw, vh);
            viewport.Inflate(4, 4);
            AreDragEndpointsVisible = viewport.Contains(source.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(source.RenderSize))) &&
                viewport.Contains(destination.TransformToAncestor(ApplicationViewport).TransformBounds(new Rect(destination.RenderSize)));

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
            // Discard the isolated draft and stop its input session before closing any remaining hosts.
            _actions?.Dispose();
            _actions = null;
            // WPF registers even unshown Windows in Application.Windows. The creation/import form
            // can be replaced by the axis editor, so closing only TrainingActions' latest references
            // leaves invisible windows keeping OnLastWindowClose (and dotnet run) alive.
            foreach (var window in _mountedContent.Keys.ToArray()) window.Close();
            _mountedContent.Clear();
        }
        #endregion END private helpers
    }
}
