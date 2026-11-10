using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Application;

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
        private IGameWorkspaceContext? _workspaceContext;
        private Action? _refreshNative;
        private bool _refreshingNative;
        private bool _refreshingChrome;
        private WorkspaceOperationKind? _previousOperation;
        private WorkspaceIdentity? _operationIdentity;
        private string? _operationSnapshot;
        private Button? _save;
        private Button? _discard;
        private TextBlock? _pending;
        private Button? _sharedDevices;

        #region BEGIN Public
        /// <summary>Gets the selected registered feature, or null before first navigation.</summary>
        public string? CurrentFeatureKey { get; private set; }
        #endregion END Public

        /// <summary>Opens the same profile-owned device manager from every game without changing game assignments.</summary>
        protected void ConfigureSharedDevices(HardwareInventoryManager? inventory, Func<Task> refresh)
        {
            if (inventory is null || Content is not UIElement original) return;
            var button = new Button { Content = "Devices…", Margin = new Thickness(8, 4, 8, 4), Padding = new Thickness(12, 4, 12, 4) };
            button.ToolTip = "Manage the connected controllers, names and preferred drawings shared by all games.";
            System.Windows.Automation.AutomationProperties.SetName(button, "Devices for all games");
            _sharedDevices = button;
            button.Click += async (_, _) =>
            {
                if (_closed || WorkspaceActionRunning) return;
                try
                {
                    WorkspaceContext.StopActivity();
                    using var dialog = new HardwareInventoryWindow(inventory, true) { Owner = this };
                    dialog.ShowDialog();
                    await inventory.ReadSnapshotAsync();
                    if (!_closed) await refresh();
                }
                catch (Exception error) { ReportWorkspaceError(error); }
            };
            var bar = new StackPanel { Orientation = Orientation.Horizontal };
            bar.Children.Add(button);
            bar.Children.Add(new TextBlock { Text = "Shared across games · game assignments are saved separately", VerticalAlignment = VerticalAlignment.Center });
            var layout = new DockPanel();
            Content = null;
            DockPanel.SetDock(bar, Dock.Top); layout.Children.Add(bar); layout.Children.Add(original);
            Content = layout;
        }

        /// <summary>Gets a mounted feature for a native refresh without replacing its pending state.</summary>
        protected UserControl? WorkspaceFeatureView(string key) => _views.GetValueOrDefault(key);

        /// <summary>Gets the common context for conformance checks and contract-typed consumers.</summary>
        internal IGameWorkspaceContext WorkspaceContext => _workspaceContext ?? throw new InvalidOperationException("Workspace context is not bound.");
        /// <summary>Binds one state authority; native callbacks only refresh panels after the shared operation releases.</summary>
        protected void BindWorkspaceContext(IGameWorkspaceContext context, Action? refreshNative = null)
        {
            if (_workspaceContext is not null) throw new InvalidOperationException("Workspace context is already bound.");
            _workspaceContext = context; _refreshNative = refreshNative;
            context.StateChanged += ContextChanged;
            RegisterWorkspaceCleanup(() => { context.StateChanged -= ContextChanged; context.Dispose(); });
        }
        /// <summary>Projects legacy native status surfaces into the common status notification contract.</summary>
        protected void BindWorkspaceStatus(TextBlock surface)
        {
            var context = WorkspaceContext;
            var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            EventHandler changed = (_, _) => context.ReportStatus(surface.Text);
            EventHandler<string> reported = (_, message) => surface.Text = message;
            descriptor.AddValueChanged(surface, changed); context.StatusChanged += reported;
            RegisterWorkspaceCleanup(() => { descriptor.RemoveValueChanged(surface, changed); context.StatusChanged -= reported; });
        }
        private void ContextChanged(object? sender, EventArgs e)
        {
            var context = _workspaceContext!;
            var operation = context.State.Operation;
            if (_previousOperation is null && operation is not null)
            {
                _operationIdentity = context.State.Identity;
                _operationSnapshot = context.CaptureSnapshot();
            }
            var released = _previousOperation is not null && operation is null;
            var nativeChanged = released && (_operationIdentity != context.State.Identity || _operationSnapshot != context.CaptureSnapshot());
            if (released) { _operationIdentity = null; _operationSnapshot = null; }
            _previousOperation = operation;
            // The notification already carries fresh authoritative state; re-reading native facts here was redundant work.
            ApplyWorkspaceChrome();
            // Failed/no-change operations update gates, not the editor's pending native choices.
            if (!nativeChanged || _refreshingNative || operation is not null || _closed) return;
            _refreshingNative = true;
            try { _refreshNative?.Invoke(); }
            finally { _refreshingNative = false; }
        }

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
        protected bool WorkspaceActionRunning => _workspaceContext?.State.Operation is not null;
        /// <summary>Gets whether the mounted session is stale after a committed save; it can only be left or replaced.</summary>
        protected bool WorkspaceRecovering => _workspaceContext?.State.Recovery.Length > 0;
        /// <summary>Gets whether shared state admits a direct edit such as a profile rename.</summary>
        protected bool WorkspaceEditable => !WorkspaceActionRunning && !WorkspaceRecovering;
        /// <summary>Gets the adapter's profile operations; every save/discard entry (button, feature, leave guard) runs this one shared sequence.</summary>
        protected abstract IGameProfileOperations ProfileOperations { get; }
        /// <summary>Refreshes adapter-specific actions after the base has refreshed common chrome.</summary>
        protected virtual void OnWorkspaceStateChanged() { }
        /// <summary>Displays an action failure through the adapter's status surface.</summary>
        protected virtual void ReportWorkspaceError(Exception exception) => System.Diagnostics.Trace.TraceError("{0}", exception);

        /// <summary>Supplies native pending-state wording while retaining the common refresh algorithm.</summary>
        protected virtual string WorkspaceStateSummary => HasWorkspaceChanges ? "Pending assignment or authoring changes" : "No pending assignment changes";
        /// <summary>Releases the mounted feature/action connections through the shared host.</summary>
        protected void ClearWorkspaceFeatures()
        {
            _host?.Clear();
            foreach (var view in _views.Values.OfType<IDisposable>()) view.Dispose();
        }

        /// <summary>Updates common pending and save/discard state, then invokes the adapter hook.</summary>
        protected void RefreshWorkspaceState()
        {
            if (_refreshingChrome) return;
            _refreshingChrome = true;
            try { _workspaceContext?.RefreshNativeState(); }
            finally { _refreshingChrome = false; }
            ApplyWorkspaceChrome();
        }

        private void ApplyWorkspaceChrome()
        {
            if (_refreshingChrome) return;
            _refreshingChrome = true;
            try
            {
                if (_save is not null) _save.IsEnabled = !WorkspaceActionRunning && CanSaveWorkspace && HasWorkspaceChanges;
                if (_discard is not null) _discard.IsEnabled = !WorkspaceActionRunning && HasWorkspaceChanges;
                if (_pending is not null) _pending.Text = WorkspaceStateSummary;
                if (_sharedDevices is not null) _sharedDevices.IsEnabled = !WorkspaceActionRunning;
                OnWorkspaceStateChanged();
            }
            finally { _refreshingChrome = false; }
        }

        /// <summary>Serializes asynchronous workspace actions and restores common availability even after failure.</summary>
        protected async Task RunWorkspaceActionAsync(Func<Task> action, WorkspaceOperationKind kind = WorkspaceOperationKind.Replace)
        {
            if (_closed) throw new InvalidOperationException("The workspace is closed.");
            var context = _workspaceContext ?? throw new InvalidOperationException("Bind the workspace context before running actions.");
            var result = await context.RunOperationAsync(kind, async _ =>
            {
                await action(); return new(WorkspaceOperationOutcome.Applied, "", kind == WorkspaceOperationKind.Save);
            }, CancellationToken.None);
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(result.Message);
            if (result.Outcome == WorkspaceOperationOutcome.Cancelled) throw new OperationCanceledException(result.Message);
        }

        /// <summary>Saves through the shared profile operations and returns the typed result: Applied (committed write) or NoChange
        /// (nothing to write). Blocked surfaces as <see cref="InvalidOperationException"/> carrying the native reason and Cancelled as
        /// <see cref="OperationCanceledException"/>, so a refused save is never reported as success and pending edits stay dirty.</summary>
        protected internal async Task<WorkspaceOperationResult> SaveWorkspaceAsync()
        {
            if (_closed) throw new InvalidOperationException("The workspace is closed.");
            var result = await ProfileOperations.SaveAsync(CancellationToken.None);
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(result.Message);
            if (result.Outcome == WorkspaceOperationOutcome.Cancelled) throw new OperationCanceledException(result.Message);
            return result;
        }

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
            if (WorkspaceActionRunning) throw new InvalidOperationException("Another workspace operation is still running. Wait for it to finish.");
            // The shared discard completes synchronously; GetResult only unwraps its result or native exception.
            var result = ProfileOperations.DiscardAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(result.Message);
        }

        /// <summary>Runs the common save/discard/cancel sequence before a game or profile switch.</summary>
        protected async Task<bool> ConfirmWorkspaceLeaveAsync(Func<GameWorkspaceLeaveDecision> choose)
        {
            if (_confirmingLeave || WorkspaceActionRunning || _closed) return false;
            _confirmingLeave = true;
            try
            {
                return await GameWorkspaceLeaveGuard.ConfirmAsync(_workspaceContext?.State, HasWorkspaceChanges, choose,
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
        protected virtual bool CanCloseWorkspace() => !WorkspaceActionRunning && (WorkspaceRecovering || !HasWorkspaceChanges || ConfirmDiscardWorkspaceChanges());

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
