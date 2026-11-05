using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Composes native assignment panels and owns common workflow, persistence feedback and event lifetime.</summary>
    public abstract class GameAssignmentsView : UserControl
    {
        private readonly ContentControl _commands = new()
            { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly ContentControl _controller = new()
            { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly ContentControl _editor = new()
            { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private Action<EventHandler>? _subscribe;
        private Action<EventHandler>? _unsubscribe;
        private bool _subscribed;
        private bool _refreshing;
        private bool _saving;
        private bool _unmounted;

        /// <summary>Assembles the shared split and editor frame before native panels are supplied.</summary>
        protected GameAssignmentsView()
        {
            var right = new Grid { Margin = new Thickness(6, 0, 0, 0) };
            right.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });
            right.RowDefinitions.Add(new() { Height = GridLength.Auto });
            Grid.SetRow(_editor, 1);
            right.Children.Add(_controller); right.Children.Add(_editor);
            Grid.SetColumn(right, 2);
            AssignmentWorkspace.Children.Add(_commands); AssignmentWorkspace.Children.Add(right);
            Content = new AdornerDecorator { Child = AssignmentWorkspace };
            Loaded += (_, _) => { _unmounted = false; Attach(); RefreshAssignments(); };
            Unloaded += (_, _) => { _unmounted = true; Detach(); };
        }

        #region BEGIN Public
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
        /// <summary>Gets the common selection state reused by adapter action policies.</summary>
        protected internal GameAssignmentSelection AssignmentSelection { get; } = new();
        /// <summary>Gets the common execution boundary shared by all native entry points.</summary>
        protected internal WorkspaceAssignmentWorkflow AssignmentWorkflow { get; } = new();
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
        /// <summary>Refreshes native drawing after successful persistence or discard.</summary>
        protected abstract void RefreshAssignmentDrawing();
        /// <summary>Displays feedback beside the adapter's editor.</summary>
        protected abstract void ShowAssignmentStatus(string message);

        /// <summary>Supplies native success wording without changing persistence orchestration.</summary>
        protected virtual string SaveSuccessMessage => "Assignments saved.";
        /// <summary>Supplies native failure wording while retaining the underlying explanation.</summary>
        protected virtual string SaveFailurePrefix => "Save failed: ";

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
            try { RefreshAssignmentsCore(); }
            finally { _refreshing = false; }
        }
        /// <summary>Gets whether native choice updates are in progress.</summary>
        protected bool AssignmentRefreshRunning => _refreshing;
        /// <summary>Combines native gates with an in-progress assignment save.</summary>
        protected WorkspaceAssignmentContext CurrentAssignmentContext
        {
            get
            {
                var native = CaptureAssignmentContext();
                return native with { Busy = native.Busy || _saving };
            }
        }
        /// <summary>Runs a native mutation against fresh common gates and draft/scope checks.</summary>
        protected DraftResult ExecuteAssignment(WorkspaceAssignmentTarget target, Func<DraftResult> apply, Func<GameAssignmentReview>? review = null) =>
            AssignmentWorkflow.Execute(new WorkspaceAssignmentHost(() => CurrentAssignmentContext, CaptureAssignmentSnapshot),
                new WorkspaceAssignmentOperation(target, review ?? (() => new(true, true, "")), apply));
        /// <summary>Reports success only after the native save completes; all failures preserve their explanation.</summary>
        protected internal async Task<bool> SaveAssignmentsAsync()
        {
            if (_saving) { ShowAssignmentStatus(SaveFailurePrefix + "Another assignment save is still running."); return false; }
            _saving = true;
            try
            {
                await SaveAssignmentsCoreAsync(); RefreshAssignmentDrawing(); ShowAssignmentStatus(SaveSuccessMessage); return true;
            }
            catch (Exception error) { ShowAssignmentStatus(SaveFailurePrefix + error.Message); return false; }
            finally { _saving = false; }
        }
        /// <summary>Reports restoration only after the native discard succeeds.</summary>
        protected internal bool DiscardAssignments()
        {
            try
            {
                if (_saving) throw new InvalidOperationException("Another assignment save is still running.");
                DiscardAssignmentsCore(); RefreshAssignmentDrawing(); ShowAssignmentStatus("Pending edits discarded."); return true;
            }
            catch (Exception error) { ShowAssignmentStatus("Discard failed: " + error.Message); return false; }
        }
        #region BEGIN private helpers
        private void Changed(object? sender, EventArgs args) => RefreshAssignments();
        private void Attach()
        {
            if (_subscribed || _subscribe is null) return;
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
