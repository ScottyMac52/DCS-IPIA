using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Composes independent command, artwork and selected-input surfaces without owning binding rules.</summary>
    public partial class EliteAssignmentsView : UserControl
    {
        #region BEGIN Public
        /// <summary>Creates the feature and connects the selected command across component namescopes.</summary>
        public EliteAssignmentsView()
        {
            InitializeComponent();
            SelectionEditor.SetEditor(_editor);
        }
        #endregion END Public

        /// <summary>Configures the shared browser using adapter-owned capabilities.</summary>
        internal void ConfigureBrowser(IReadOnlyList<GameCommandTypeOption> inputTypes, IReadOnlyList<GameCommandStatusOption> statuses) =>
            CommandBrowser.Configure(inputTypes, statuses);
        internal GameAssignmentEditorView Selection => SelectionEditor;
        private readonly EliteAssignmentEditorView _editor = new();
        #region BEGIN private helpers
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
