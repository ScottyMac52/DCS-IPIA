using System.Windows.Controls;
using System.Windows.Data;

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
            Editor.AssignmentCommand.SetBinding(TextBlock.TextProperty, new Binding("SelectedItem.DisplayName")
            {
                Source = CommandBrowser.CommandsList, TargetNullValue = "No command selected.", FallbackValue = "No command selected."
            });
        }
        #endregion END Public

        #region BEGIN private helpers
        internal ComboBox CommandCategoryPicker => CommandBrowser.CommandCategoryPicker;
        internal ComboBox CommandTypePicker => CommandBrowser.CommandTypePicker;
        internal Button ClearCommandFiltersButton => CommandBrowser.ClearCommandFiltersButton;
        internal ComboBox AssignmentStatusPicker => CommandBrowser.AssignmentStatusPicker;
        internal TextBox CommandSearch => CommandBrowser.CommandSearch;
        internal TextBlock CommandCount => CommandBrowser.CommandCount;
        internal TextBlock CommandEmptyHint => CommandBrowser.CommandEmptyHint;
        internal ListBox CommandsList => CommandBrowser.CommandsList;
        internal TextBlock AssignmentDraftStatus => Editor.AssignmentDraftStatus;
        internal StackPanel AssignmentEditor => Editor.AssignmentEditor;
        internal TextBlock AssignmentCommand => Editor.AssignmentCommand;
        internal ComboBox InputKey => Editor.InputKey;
        internal ComboBox SlotPicker => Editor.SlotPicker;
        internal ComboBox ModePicker => Editor.ModePicker;
        internal StackPanel AxisTools => Editor.AxisTools;
        internal CheckBox InvertAxis => Editor.InvertAxis;
        internal TextBox AxisDeadzone => Editor.AxisDeadzone;
        internal Expander ModifierTools => Editor.ModifierTools;
        internal ComboBox ModifierDevicePicker => Editor.ModifierDevicePicker;
        internal ComboBox ModifierKey => Editor.ModifierKey;
        internal Button AddCombinationButton => Editor.AddCombinationButton;
        internal Button RemoveCombinationButton => Editor.RemoveCombinationButton;
        internal ListBox ModifiersList => Editor.ModifiersList;
        internal CheckBox CommandToggle => Editor.CommandToggle;
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
