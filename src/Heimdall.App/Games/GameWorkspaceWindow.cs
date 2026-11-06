using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Owns reusable feature navigation, guarded closing and lifetime cleanup without native game state.</summary>
    public abstract class GameWorkspaceWindow : Window
    {
        private readonly Dictionary<string, WorkspaceFeature> _features = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UserControl> _views = new(StringComparer.Ordinal);
        private readonly List<Action> _cleanup = [];
        private GameWorkspaceFeatureHost? _host;
        private Action<UserControl>? _prepare;
        private Action<string>? _selected;
        private bool _closed;
        private bool _confirmingLeave;
        private bool _runningAction;
        private Button? _save;
        private Button? _discard;
        private TextBlock? _pending;

        #region BEGIN Public
        /// <summary>Gets the selected registered feature, or null before first navigation.</summary>
        public string? CurrentFeatureKey { get; private set; }
        #endregion END Public

        /// <summary>Installs the feature registrations once, after the derived XAML has initialized.</summary>
        protected void ConfigureWorkspace(ContentControl content, FeatureActionBar actions,
            IEnumerable<WorkspaceFeature> features, Action<UserControl>? prepare = null, Action<string>? selected = null)
        {
            if (_host is not null) throw new InvalidOperationException("Workspace features are already configured.");
            var registrations = features.ToArray();
            if (registrations.Any(feature => string.IsNullOrWhiteSpace(feature.Key)) ||
                registrations.Select(feature => feature.Key).Distinct(StringComparer.Ordinal).Count() != registrations.Length)
                throw new ArgumentException("Feature keys must be nonempty and unique.", nameof(features));
            foreach (var feature in registrations) _features.Add(feature.Key, feature);
            _host = new(content, actions);
            _prepare = prepare;
            _selected = selected;
        }

        /// <summary>Selects a registered feature, preserving each mounted view's own state.</summary>
        protected internal void NavigateToFeature(string key)
        {
            if (_closed) return;
            if (_host is null || !_features.TryGetValue(key, out var feature))
                throw new ArgumentException("Unknown workspace feature: " + key, nameof(key));
            CurrentFeatureKey = key;
            _selected?.Invoke(key);
            // Re-evaluate availability even for cached views; entitlement can change while a window is open.
            if (feature.UnavailableReason?.Invoke() is { } reason)
            {
                _host.Show(new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap });
                return;
            }
            if (!_views.TryGetValue(key, out var view))
            {
                view = feature.Create();
                _views.Add(key, view);
            }
            _host.Show(view, _prepare);
        }

        /// <summary>Registers window-owned subscriptions and resources for reverse-order release on a successful close.</summary>
        protected void RegisterWorkspaceCleanup(Action cleanup)
        {
            if (_closed) throw new InvalidOperationException("The workspace is closed.");
            _cleanup.Add(cleanup);
        }

        /// <summary>Connects common command chrome to the base save/discard implementations.</summary>
        protected void ConfigureWorkspaceCommands(Button save, Button discard, TextBlock pending)
        {
            _save = save; _discard = discard; _pending = pending;
            RefreshWorkspaceState();
        }

        /// <summary>Reports pending state using the adapter's own native session.</summary>
        protected abstract bool HasWorkspaceChanges { get; }
        /// <summary>Reports native save availability for common chrome and leave prompts.</summary>
        protected abstract bool CanSaveWorkspace { get; }
        /// <summary>Gets whether a common asynchronous action is executing.</summary>
        protected bool WorkspaceActionRunning => _runningAction;
        /// <summary>Validates native write access and supplies persistence, throwing a specific reason on rejection.</summary>
        protected abstract Task SaveWorkspaceCoreAsync();
        /// <summary>Supplies the adapter's restoration operation.</summary>
        protected abstract void DiscardWorkspaceCore();
        /// <summary>Refreshes adapter-specific actions after the base has refreshed common chrome.</summary>
        protected virtual void OnWorkspaceStateChanged() { }
        /// <summary>Displays an action failure through the adapter's status surface.</summary>
        protected virtual void ReportWorkspaceError(Exception exception) => System.Diagnostics.Trace.TraceError("{0}", exception);

        /// <summary>Supplies native pending-state wording while retaining the common refresh algorithm.</summary>
        protected virtual string WorkspaceStateSummary => HasWorkspaceChanges ? "Pending assignment or authoring changes" : "No pending assignment changes";
        /// <summary>Releases the mounted feature/action connections through the shared host.</summary>
        protected void ClearWorkspaceFeatures() => _host?.Clear();

        /// <summary>Updates common pending and save/discard state, then invokes the adapter hook.</summary>
        protected void RefreshWorkspaceState()
        {
            if (_save is not null) _save.IsEnabled = !_runningAction && CanSaveWorkspace && HasWorkspaceChanges;
            if (_discard is not null) _discard.IsEnabled = !_runningAction && HasWorkspaceChanges;
            if (_pending is not null) _pending.Text = WorkspaceStateSummary;
            OnWorkspaceStateChanged();
        }

        /// <summary>Serializes asynchronous workspace actions and restores common availability even after failure.</summary>
        protected async Task RunWorkspaceActionAsync(Func<Task> action)
        {
            if (_closed) throw new InvalidOperationException("The workspace is closed.");
            if (_runningAction) throw new InvalidOperationException("Another workspace operation is still running. Wait for it to finish.");
            _runningAction = true;
            try
            {
                RefreshWorkspaceState();
                await action();
            }
            finally { _runningAction = false; RefreshWorkspaceState(); }
        }

        /// <summary>Saves through the adapter hook using the common action lifetime.</summary>
        protected internal Task SaveWorkspaceAsync() => RunWorkspaceActionAsync(SaveWorkspaceCoreAsync);

        /// <summary>Executes the shared Save button path and displays failures without losing pending state.</summary>
        protected async void SaveClicked(object sender, RoutedEventArgs e)
        {
            try { await SaveWorkspaceAsync(); }
            catch (Exception error) { ReportWorkspaceError(error); }
        }

        /// <summary>Executes the shared Discard button path through the adapter's restoration hook.</summary>
        protected internal void DiscardClicked(object sender, RoutedEventArgs e)
        {
            if (_closed) throw new InvalidOperationException("The workspace is closed.");
            if (_runningAction) throw new InvalidOperationException("Another workspace operation is still running. Wait for it to finish.");
            DiscardWorkspaceCore();
            RefreshWorkspaceState();
        }

        /// <summary>Runs the common save/discard/cancel sequence before a game or profile switch.</summary>
        protected async Task<bool> ConfirmWorkspaceLeaveAsync(Func<GameWorkspaceLeaveDecision> choose)
        {
            if (_confirmingLeave || _runningAction || _closed) return false;
            _confirmingLeave = true;
            try
            {
                return await GameWorkspaceLeaveGuard.ConfirmAsync(HasWorkspaceChanges, choose,
                    async () =>
                    {
                        if (!CanSaveWorkspace) return false;
                        try { await SaveWorkspaceAsync(); return !HasWorkspaceChanges; }
                        catch (Exception error) { ReportWorkspaceError(error); return false; }
                    }, () => DiscardClicked(this, new()));
            }
            finally { _confirmingLeave = false; }
        }

        /// <summary>Supplies adapter wording or an injected choice for a close with pending edits.</summary>
        protected virtual bool ConfirmDiscardWorkspaceChanges() => ThemeMessageBox.Show(this,
            "Discard unsaved workspace changes?", "Pending changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

        /// <summary>Guards closing with shared pending and operation state.</summary>
        protected virtual bool CanCloseWorkspace() => !_runningAction && (!HasWorkspaceChanges || ConfirmDiscardWorkspaceChanges());

        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_confirmingLeave || !CanCloseWorkspace()) e.Cancel = true;
            base.OnClosing(e);
        }

        /// <inheritdoc/>
        protected override void OnClosed(EventArgs e)
        {
            if (!_closed)
            {
                _closed = true;
                try { ClearWorkspaceFeatures(); }
                catch (Exception exception) { System.Diagnostics.Trace.TraceError("Workspace feature cleanup failed: {0}", exception); }
                finally
                {
                    try
                    {
                        for (var index = _cleanup.Count - 1; index >= 0; index--)
                        {
                            try { _cleanup[index](); }
                            catch (Exception error) { System.Diagnostics.Trace.TraceError("Workspace cleanup failed: {0}", error); }
                        }
                    }
                    finally { _cleanup.Clear(); _views.Clear(); _features.Clear(); _prepare = null; _selected = null; }
                }
            }
            base.OnClosed(e);
        }
    }
}
