using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Composes independent command, artwork and selected-input surfaces without owning binding rules.</summary>
    public partial class EliteAssignmentsView : GameAssignmentsView
    {
        #region BEGIN Public
        /// <summary>Creates the feature and connects the selected command across component namescopes.</summary>
        public EliteAssignmentsView()
        {
            InitializeComponent();
            SelectionEditor.SetEditor(_editor);
            AssignmentWorkspace.Name = "AuthoringPanel";
            RegisterName("AuthoringPanel", AssignmentWorkspace);
            WorkspaceLayout.SetKey(AssignmentWorkspace, "elite.assignments.split");
            AssignmentInspectorViewport.Name = "AssignmentInspectorScroll";
            RegisterName("AssignmentInspectorScroll", AssignmentInspectorViewport);
            ControllerReserveHeight = 240;
            Artwork.Loaded += (_, _) => UpdateControllerReservation();
            Artwork.DiagramScroll.SizeChanged += (_, _) => UpdateControllerReservation();
        }
        #endregion END Public

        /// <summary>Configures the shared browser using adapter-owned capabilities.</summary>
        internal void ConfigureBrowser(IReadOnlyList<GameCommandTypeOption> inputTypes, IReadOnlyList<GameCommandStatusOption> statuses) =>
            CommandBrowser.Configure(inputTypes, statuses);
        internal GameAssignmentEditorView Selection => SelectionEditor;
        private readonly EliteAssignmentEditorView _editor = new();
        private EliteWorkspaceContext? _context;
        private Action? _refresh;
        private Action? _availability;
        private Action? _drawingRefresh;
        private Action<string>? _status;
        private Func<Task>? _save;
        private Action? _discard;
        internal void Initialize(EliteWorkspaceContext context, Action refresh, Action availability, Action drawingRefresh,
            Func<Task> save, Action discard, Action<string> status)
        {
            _context = context; _refresh = refresh; _availability = availability; _drawingRefresh = drawingRefresh;
            _save = save; _discard = discard; _status = status;
            BindAssignmentContext(context);
        }
        internal void RefreshNativeAssignments() => SynchronizeNativeAssignments();
        protected override WorkspaceAssignmentContext CaptureAssignmentContext() => _context?.AssignmentContext ?? new("", false, false, false, false);
        protected override Task SaveAssignmentsCoreAsync() => _save?.Invoke() ?? throw new InvalidOperationException("Open an Elite workspace before saving.");
        protected override void DiscardAssignmentsCore()
        {
            if (_discard is null) throw new InvalidOperationException("Open an Elite workspace before discarding.");
            _discard();
        }
        protected override void RefreshAssignmentsCore() => _refresh?.Invoke();
        protected override void RefreshAssignmentAvailability() => _availability?.Invoke();
        protected override void RefreshAssignmentDrawing() => _drawingRefresh?.Invoke();
        protected override void ShowAssignmentStatus(string message)
        {
            AssignmentDraftStatus.Text = message; _status?.Invoke(message);
        }
        #region BEGIN private helpers
        private void UpdateControllerReservation()
        {
            var viewport = Artwork.DiagramScroll;
            if (!Artwork.IsLoaded || viewport.Visibility != System.Windows.Visibility.Visible || viewport.ActualHeight <= 0) return;
            var chrome = Artwork.ActualHeight - viewport.ActualHeight;
            ControllerReserveHeight = Math.Max(180, chrome + 160);
        }
        internal ComboBox CommandCategoryPicker => CommandBrowser.CommandCategoryPicker;
        internal ComboBox CommandTypePicker => CommandBrowser.CommandTypePicker;
        internal Button ClearCommandFiltersButton => CommandBrowser.ClearCommandFiltersButton;
        internal ComboBox AssignmentStatusPicker => CommandBrowser.AssignmentStatusPicker;
        internal TextBox CommandSearch => CommandBrowser.CommandSearch;
        internal TextBlock CommandCount => CommandBrowser.CommandCount;
        internal TextBlock CommandEmptyHint => CommandBrowser.CommandEmptyHint;
        internal ListBox CommandsList => CommandBrowser.CommandsList;
        internal TextBlock AssignmentDraftStatus => SelectionEditor.AssignmentDraftStatus;
        internal StackPanel AssignmentEditor => _editor.AssignmentEditor;
        internal TextBlock AssignmentCommand => SelectionEditor.AssignmentCommand;
        internal ComboBox InputKey => _editor.InputKey;
        internal TextBlock CommandSlotSummary => _editor.CommandSlotSummary;
        internal TextBlock AxisSlotLabel => _editor.AxisSlotLabel;
        internal ComboBox SlotPicker => _editor.SlotPicker;
        internal ComboBox ModePicker => _editor.ModePicker;
        internal StackPanel AxisTools => _editor.AxisTools;
        internal CheckBox InvertAxis => _editor.InvertAxis;
        internal TextBox AxisDeadzone => _editor.AxisDeadzone;
        internal Expander ModifierTools => _editor.ModifierTools;
        internal ComboBox ModifierDevicePicker => _editor.ModifierDevicePicker;
        internal ComboBox ModifierKey => _editor.ModifierKey;
        internal Button AddCombinationButton => _editor.AddCombinationButton;
        internal Button RemoveCombinationButton => _editor.RemoveCombinationButton;
        internal ListBox ModifiersList => _editor.ModifiersList;
        internal CheckBox CommandToggle => _editor.CommandToggle;
        internal Button ExportSheetButton => Artwork.ExportSheetButton;
        internal Slider DiagramZoom => Artwork.DiagramZoom;
        internal TextBlock InputActivityHint => Artwork.InputActivityHint;
        internal TextBox InputActivity => Artwork.InputActivity;
        internal ScrollViewer DiagramScroll => Artwork.DiagramScroll;
        internal TextBlock DiagramHint => Artwork.DiagramHint;
        internal Canvas DiagramCanvas => Artwork.DiagramCanvas;
        internal DataGrid AssignmentsGrid => Artwork.AssignmentsGrid;
        #endregion END private helpers
    }
}

