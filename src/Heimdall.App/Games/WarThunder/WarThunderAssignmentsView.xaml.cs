using System.Windows.Controls;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Owns the simulator-native feature surface composed by the shared workspace host.</summary>
    public partial class WarThunderAssignmentsView : GameAssignmentsView
    {
        /// <summary>Creates the themed feature without owning session or native editing rules.</summary>
        public WarThunderAssignmentsView()
        {
            InitializeComponent();
            WorkspaceLayout.SetKey(AssignmentWorkspace, "squadrons.assignments.split");
            ControllerReserveHeight = 240;
            Artwork.Loaded += (_, _) => UpdateControllerReservation();
            Artwork.Viewport.SizeChanged += (_, _) => UpdateControllerReservation();
            Artwork.Feedback.Text = "Choose a command and input below, then Apply assignment. Native input indices are zero-based.";
        }
        private WarThunderWorkspaceContext? _context;
        private Action? _refresh;
        private Action? _drawingRefresh;
        private Action<string>? _status;
        private Func<Task>? _save;
        private Action? _discard;
        internal void Initialize(WarThunderWorkspaceContext context, Action refresh, Action drawingRefresh,
            Func<Task> save, Action discard, Action<string> status)
        {
            _context = context; _refresh = refresh; _drawingRefresh = drawingRefresh;
            _save = save; _discard = discard; _status = status;
            BindAssignmentChanges(handler => context.Changed += handler, handler => context.Changed -= handler);
        }
        internal void RefreshNativeAssignments() => RefreshAssignments();
        internal WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action, bool ready, string reason = "") =>
            WorkspaceAssignmentActionState.For(action, CurrentAssignmentContext, new(true, ready, reason));
        internal DraftResult ExecuteNativeAssignment(string command, string input, string alternative, Func<GameAssignmentReview> review, Action apply) =>
            ExecuteAssignment(new(CurrentAssignmentContext.Scope, command, input, alternative),
                () => { apply(); return new(DraftOutcome.Applied, ""); }, review);
        protected override WorkspaceAssignmentContext CaptureAssignmentContext() => _context?.AssignmentContext ?? new("", false, false, false, false);
        protected override string CaptureAssignmentSnapshot() => JsonSerializer.Serialize(_context?.Session?.Snapshot);
        protected override Task SaveAssignmentsCoreAsync() => _save?.Invoke() ?? throw new InvalidOperationException("Open a WarThunder workspace before saving.");
        protected override void DiscardAssignmentsCore()
        {
            if (_discard is null) throw new InvalidOperationException("Open a WarThunder workspace before discarding.");
            _discard();
        }
        protected override void RefreshAssignmentsCore() => _refresh?.Invoke();
        protected override void RefreshAssignmentDrawing() => _drawingRefresh?.Invoke();
        protected override void ShowAssignmentStatus(string message)
        {
            Editor.Frame.AssignmentDraftStatus.Text = message; _status?.Invoke(message);
        }
        private void UpdateControllerReservation()
        {
            if (!Artwork.IsLoaded || Artwork.Viewport.Visibility != System.Windows.Visibility.Visible || Artwork.Viewport.ActualHeight <= 0) return;
            ControllerReserveHeight = Math.Max(180, DrawingPanel.ActualHeight + DrawingPanel.Margin.Top + DrawingPanel.Margin.Bottom - Artwork.Viewport.ActualHeight + 160);
        }
        internal ScrollViewer InspectorViewport => AssignmentInspectorViewport;
        internal Slider Zoom => Artwork.Zoom;
        internal WorkspaceAssignmentContext ActionContext => CurrentAssignmentContext;
        internal TextBlock DrawingHint => Artwork.Hint;
        internal TextBox Activity => Artwork.Activity;
        internal ScrollViewer DrawingScroll => Artwork.Viewport;
        internal Canvas DrawingCanvas => Artwork.Surface;
    }
}

