using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games
{
    /// <summary>Composes native assignment panels and owns common workflow, persistence feedback and event lifetime.</summary>
    public abstract class GameAssignmentsView : UserControl, IDisposable
    {
        private readonly ContentControl _commands = new()
        { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly ContentControl _controller = new()
        { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly ContentControl _editor = new()
        { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly Grid _right = new() { Margin = new Thickness(6, 0, 0, 0) };
        private readonly ScrollViewer _editorViewport = new()
        { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false };
        private Action<EventHandler>? _subscribe;
        private Action<EventHandler>? _unsubscribe;
        private bool _subscribed;
        private bool _refreshing;
        private IGameWorkspaceContext? _context;
        private bool _unmounted;
        private WorkspaceChangeObserver? _observer;
        private IGameAssignmentBrowser? _browser;
        private bool _disposed;

        /// <summary>Assembles the shared split and editor frame before native panels are supplied.</summary>
        protected GameAssignmentsView()
        {
            _right.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });
            _right.RowDefinitions.Add(new() { Height = GridLength.Auto });
            _editorViewport.Content = _editor;
            Grid.SetRow(_editorViewport, 1);
            _right.Children.Add(_controller); _right.Children.Add(_editorViewport);
            _right.SizeChanged += (_, _) => UpdatePanelAllocation();
            Grid.SetColumn(_right, 2);
            AssignmentWorkspace.Children.Add(_commands); AssignmentWorkspace.Children.Add(_right);
            Content = new AdornerDecorator { Child = AssignmentWorkspace };
            Loaded += (_, _) => { _unmounted = false; Attach(); _browser?.SetActive(true); RefreshAssignments(); };
            Unloaded += (_, _) => { _unmounted = true; _browser?.SetActive(false); Detach(); };
        }

        #region BEGIN Public
        /// <summary>Gets completed native assignment-panel refresh passes for interaction diagnostics.</summary>
        internal int NativeRefreshPasses { get; private set; }

        /// <summary>Detaches mounted notifications and releases the browser when its owning workspace closes.</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _unmounted = true; Detach(); _browser?.Dispose(); _browser = null; DisposeNative(); GC.SuppressFinalize(this); }

        /// <summary>Identifies the requested controller-panel reservation before the inspector may grow.</summary>
        public static readonly DependencyProperty ControllerReserveHeightProperty = DependencyProperty.Register(
            nameof(ControllerReserveHeight), typeof(double), typeof(GameAssignmentsView),
            new PropertyMetadata(180d, (owner, _) => ((GameAssignmentsView)owner).UpdatePanelAllocation()),
            value => double.IsFinite((double)value) && (double)value >= 0);
        /// <summary>Gets or sets controller space to preserve when possible; the inspector scrolls within the remaining height.</summary>
        public double ControllerReserveHeight
        {
            get => (double)GetValue(ControllerReserveHeightProperty);
            set => SetValue(ControllerReserveHeightProperty, value);
        }
        /// <summary>Identifies the game-supplied command browser panel.</summary>
        public static readonly DependencyProperty CommandsPanelProperty = DependencyProperty.Register(
            nameof(CommandsPanel), typeof(UIElement), typeof(GameAssignmentsView),
            new PropertyMetadata(null, (owner, args) => ((GameAssignmentsView)owner)._commands.Content = args.NewValue));
        /// <summary>Identifies the game-supplied controller/drawing panel.</summary>
        public static readonly DependencyProperty ControllerPanelProperty = DependencyProperty.Register(
            nameof(ControllerPanel), typeof(UIElement), typeof(GameAssignmentsView),
            new PropertyMetadata(null, (owner, args) => ((GameAssignmentsView)owner)._controller.Content = args.NewValue));
        /// <summary>Identifies the game-supplied selected-input editor panel.</summary>
        public static readonly DependencyProperty EditorPanelProperty = DependencyProperty.Register(
            nameof(EditorPanel), typeof(UIElement), typeof(GameAssignmentsView),
            new PropertyMetadata(null, (owner, args) => ((GameAssignmentsView)owner)._editor.Content = args.NewValue));
        /// <summary>Gets or sets the native command browser.</summary>
        public UIElement? CommandsPanel { get => (UIElement?)GetValue(CommandsPanelProperty); set => SetValue(CommandsPanelProperty, value); }
        /// <summary>Gets or sets the native controller presentation.</summary>
        public UIElement? ControllerPanel { get => (UIElement?)GetValue(ControllerPanelProperty); set => SetValue(ControllerPanelProperty, value); }
        /// <summary>Gets or sets the native selected-input editor.</summary>
        public UIElement? EditorPanel { get => (UIElement?)GetValue(EditorPanelProperty); set => SetValue(EditorPanelProperty, value); }
        #endregion END Public

        /// <summary>Gets the common geometry for native drag feedback and persisted splitter configuration.</summary>
        protected GameWorkspaceSplit AssignmentWorkspace { get; } = new() { Margin = new Thickness(4), Background = System.Windows.Media.Brushes.Transparent };
        /// <summary>Gets the common inspector viewport for native namescope compatibility and focus checks.</summary>
        protected ScrollViewer AssignmentInspectorViewport => _editorViewport;
        /// <summary>Gets the common selection state reused by adapter action policies.</summary>
        protected internal GameAssignmentSelection AssignmentSelection => _context is IWorkspaceSelectionEngineOwner owner ? owner.SelectionEngine : throw new InvalidOperationException("Use the bound context selection contract.");
        /// <summary>Gets the common execution boundary shared by all native entry points.</summary>
        protected internal WorkspaceAssignmentWorkflow AssignmentWorkflow => _context?.AssignmentWorkflow ?? throw new InvalidOperationException("Bind a workspace context before assigning.");
        /// <summary>Reads fresh native editing gates and scope.</summary>
        protected abstract WorkspaceAssignmentContext CaptureAssignmentContext();
        /// <summary>Captures native draft identity for review/change checks.</summary>
        protected abstract string CaptureAssignmentSnapshot();
        /// <summary>Validates native write access and persists through the workspace owner.</summary>
        protected abstract Task SaveAssignmentsCoreAsync();
        /// <summary>Restores the native saved baseline through the workspace owner.</summary>
        protected abstract void DiscardAssignmentsCore();
        /// <summary>Refreshes native panels from authoritative state.</summary>
        protected abstract void RefreshAssignmentsCore();
        /// <summary>Updates action gates without loading saved values over a pending native editor.</summary>
        protected abstract void RefreshAssignmentAvailability();
        /// <summary>Refreshes native drawing after successful persistence or discard.</summary>
        protected abstract void RefreshAssignmentDrawing();
        /// <summary>Displays feedback beside the adapter's editor.</summary>
        protected abstract void ShowAssignmentStatus(string message);

        /// <summary>Supplies native success wording without changing persistence orchestration.</summary>
        protected virtual string SaveSuccessMessage => "Assignments saved.";
        /// <summary>Supplies native failure wording while retaining the underlying explanation.</summary>
        protected virtual string SaveFailurePrefix => "Save failed: ";

        /// <summary>Releases additional native resources when the workspace closes.</summary>
        protected virtual void DisposeNative() { }
        /// <summary>Gets the bound role for production conformance/work measurements.</summary>
        internal IGameAssignmentBrowser? BoundBrowser => _browser;
        /// <summary>Shares browser activation across cached native feature views through the role contract.</summary>
        internal void BindBrowser(IGameAssignmentBrowser browser)
        {
            _browser?.SetActive(false); _browser = browser; browser.SetActive(IsLoaded);
        }
        /// <summary>Shares the context engine and notification lifetime rather than creating view-owned state.</summary>
        protected void BindAssignmentContext(IGameWorkspaceContext context)
        {
            _context = context; _observer = new(context);
            BindAssignmentChanges(handler => context.StateChanged += handler, handler => context.StateChanged -= handler);
        }
        /// <summary>Rebinds state notifications without retaining the previous workspace or duplicating subscriptions.</summary>
        protected void BindAssignmentChanges(Action<EventHandler> subscribe, Action<EventHandler> unsubscribe)
        {
            Detach(); _subscribe = subscribe; _unsubscribe = unsubscribe;
            if (!_unmounted) Attach();
        }
        /// <summary>Prevents recursive selection notifications during native refresh.</summary>
        protected void RefreshAssignments()
        {
            if (_refreshing) return;
            _refreshing = true;
            try { RefreshAssignmentsCore(); NativeRefreshPasses++; RefreshAssignmentAvailability(); ObserveContext(); }
            finally { _refreshing = false; }
        }
        /// <summary>Gets whether native choice updates are in progress.</summary>
        protected bool AssignmentRefreshRunning => _refreshing;
        /// <summary>Combines native gates with an in-progress assignment save.</summary>
        protected WorkspaceAssignmentContext CurrentAssignmentContext => CaptureAssignmentContext();
        /// <summary>Runs a native mutation against fresh common gates and draft/scope checks.</summary>
        protected DraftResult ExecuteAssignment(WorkspaceAssignmentTarget target, Func<DraftResult> apply, Func<GameAssignmentReview>? review = null)
        {
            // Native sessions notify their context before the window rebuilds its rows. Publish only after all
            // native change handlers finish, so editors cannot reload the previous binding during the mutation.
            using var refresh = _context?.DeferRefresh();
            return AssignmentWorkflow.Execute(new WorkspaceAssignmentHost(() => CurrentAssignmentContext, () => _context?.CaptureSnapshot() ?? CaptureAssignmentSnapshot()),
                new WorkspaceAssignmentOperation(target, review ?? (() => new(true, true, "")), apply));
        }
        /// <summary>Reports success only after the native save completes; all failures preserve their explanation.</summary>
        protected internal async Task<bool> SaveAssignmentsAsync()
        {
            if (_context?.State.Operation is not null) { ShowAssignmentStatus(SaveFailurePrefix + "Another assignment save is still running."); return false; }
            try
            {
                await SaveAssignmentsCoreAsync(); RefreshAssignmentDrawing(); ShowAssignmentStatus(SaveSuccessMessage); return true;
            }
            catch (Exception error) { ShowAssignmentStatus(SaveFailurePrefix + error.Message); return false; }
        }
        /// <summary>Reports restoration only after the native discard succeeds.</summary>
        protected internal bool DiscardAssignments()
        {
            try
            {
                if (_context?.State.Operation is not null) throw new InvalidOperationException("Another assignment save is still running.");
                DiscardAssignmentsCore(); RefreshAssignmentDrawing(); ShowAssignmentStatus("Pending edits discarded."); return true;
            }
            catch (Exception error) { ShowAssignmentStatus("Discard failed: " + error.Message); return false; }
        }
        #region BEGIN private helpers
        private void UpdatePanelAllocation()
        {
            var available = _right.ActualHeight;
            if (available <= 0) return;
            // Keep the inspector reachable even when the entire pane is shorter than the requested reservation.
            _editorViewport.MaxHeight = Math.Min(available, Math.Max(32, available - ControllerReserveHeight));
        }
        private void Changed(object? sender, EventArgs args)
        {
            if (_refreshing) return;
            // A context notification is not a request to reload the native editor. Pending inputs and names,
            // activity gates and failed operations must preserve its fields; actual native changes reload it.
            if (_context is { } context)
            {
                if (context.State.Operation is not null) { RefreshAvailabilityOnly(); return; }
                // The shared content stamp replaces serializing the native document on every notification.
                if (_observer!.Observe() != WorkspaceChangeKind.Content) { RefreshAvailabilityOnly(); return; }
            }
            RefreshAssignments();
        }
        private void RefreshAvailabilityOnly()
        {
            _refreshing = true;
            try { RefreshAssignmentAvailability(); }
            finally { _refreshing = false; }
        }
        private void ObserveContext()
        {
            _observer?.Accept();
        }
        private void Attach()
        {
            if (_subscribed || _subscribe is null) return;
            ObserveContext();
            _subscribe(Changed); _subscribed = true;
        }
        private void Detach()
        {
            if (!_subscribed) return;
            _unsubscribe?.Invoke(Changed); _subscribed = false;
        }
        #endregion END private helpers
    }
}
