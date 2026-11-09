using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Owns ED command filtering, grouping and command/slot selection.</summary>
    internal sealed class EliteAssignmentBrowser(
        EliteWorkspaceContext context,
        EliteAssignmentsView _assignmentsView,
        Action<string> Status) : Heimdall.EliteDangerous.EliteAssignmentBrowser(context, context.Backend)
    {
        private readonly GameCommandDragSource _commandDrag = new(_assignmentsView.CommandsList, "heimdall.elite.command", () => context.Session?.AccessMessage.Length == 0);
        private string? CommandCategory => _assignmentsView.CommandCategoryPicker.SelectedItem is string category && category != "All categories" ? category : null;
        private CommandType? InputType => (_assignmentsView.CommandTypePicker.SelectedItem as GameCommandTypeOption)?.Type;
        private GameProfileSession? _session => context.Session;
        private bool _refreshing { get => context.Refreshing; set => context.Refreshing = value; }
        private IReadOnlyList<SimCommand> _commands => context.Commands;
        private IReadOnlyList<EliteAssignmentRow> _rows => context.Rows;
        private GameAssignmentSelection _selection => context.Selection;
        private GameDeviceChoice? Device => context.Device;
        private SimCommand? Command => context.Command;
        private string Section => context.Section;
        private string Slot => context.Selection.Slot;
        #region BEGIN Public
        internal void FilterCommands(string? selected = null)
        {
            var previous = _refreshing; _refreshing = true;
            try
            {
                Query(new(Section, Device?.Id, CommandCategory, InputType,
                    (_assignmentsView.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Status ?? GameCommandAssignmentStatus.All),
                    _assignmentsView.CommandSearch.Text, selected);
                Publish();
                if (Filter.Category != CommandCategory) Query(Filter with { Category = CommandCategory }, _assignmentsView.CommandSearch.Text, selected);
            }
            finally { _refreshing = previous; }
            _assignmentsView.SynchronizeNativeAssignments();
        }

        protected override void OnProjectionChanged() => Publish();
        internal void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }

        internal void CommandFilterChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }

        internal void ClearCommandFiltersClicked(object sender, RoutedEventArgs e) => ClearCommandFilters(Command?.Id);

        internal void CommandChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            SelectCommand((_assignmentsView.CommandsList.SelectedItem as SimCommand)?.Id);
            Publish(); _assignmentsView.SynchronizeNativeAssignments();
        }

        internal void SlotChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            context.Selection.SelectSlot(_assignmentsView.SlotPicker.SelectedIndex == 1 ? "Secondary" : "Primary");
            _assignmentsView.SynchronizeNativeAssignments();
        }

        internal void AssignmentSelected(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _assignmentsView.AssignmentsGrid.SelectedItem is EliteAssignmentRow row) SelectRow(row); }

        internal void SelectRow(EliteAssignmentRow row)
        {
            if (!_assignmentsView.CommandsList.Items.Cast<SimCommand>().Any(x => x.Id == row.CommandId))
            {
                ClearCommandFilters(row.CommandId);
                Status("Command filters cleared to show the selected assignment.");
            }
            SelectRecord(Heimdall.EliteDangerous.EliteAssignmentBrowser.Address(row));
            _refreshing = true; _assignmentsView.CommandsList.SelectedItem = _assignmentsView.CommandsList.Items.Cast<SimCommand>().FirstOrDefault(x => x.Id == row.CommandId); _assignmentsView.SlotPicker.SelectedIndex = row.Slot == "Secondary" ? 1 : 0; _refreshing = false;
            _assignmentsView.SynchronizeNativeAssignments();
        }
        /// <summary>Detaches native command drag handlers.</summary>
        protected override void DisposeNative() => _commandDrag.Dispose();
        #endregion END Public
        #region BEGIN private helpers
        private void Publish()
        {
            var previous = _refreshing; _refreshing = true;
            try
            {
                context.Commands = AllCommands; context.Rows = AllRecords;
                var categories = Categories.Prepend("All categories").ToArray();
                var category = CommandCategory;
                if (!(_assignmentsView.CommandCategoryPicker.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(categories))
                {
                    _assignmentsView.CommandCategoryPicker.ItemsSource = categories;
                    _assignmentsView.CommandCategoryPicker.SelectedItem = categories.Contains(category) ? category : categories[0];
                }
                if (!ReferenceEquals(_publishedCommands, Commands))
                {
                    _publishedCommands = Commands;
                    var view = new ListCollectionView(Commands.ToList());
                    view.GroupDescriptions.Add(new PropertyGroupDescription("Metadata[ed.category]"));
                    _assignmentsView.CommandsList.ItemsSource = view;
                }
                _assignmentsView.CommandsList.SelectedItem = Commands.FirstOrDefault(x => x.Id == context.State.Selection.CommandId);
                if (context.State.Selection.SlotId is "Primary" or "Secondary") _assignmentsView.SlotPicker.SelectedIndex = context.State.Selection.SlotId == "Secondary" ? 1 : 0;
                if (!ReferenceEquals(_assignmentsView.AssignmentsGrid.ItemsSource, Records)) _assignmentsView.AssignmentsGrid.ItemsSource = Records;
                _assignmentsView.CommandCount.Text = Commands.Count + " of " + AllCommands.Count + " commands";
                _assignmentsView.CommandEmptyHint.Visibility = Commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally { _refreshing = previous; }
        }
        private IReadOnlyList<SimCommand>? _publishedCommands;


        private void ClearCommandFilters(string? selected)
        {
            _refreshing = true; _assignmentsView.AssignmentStatusPicker.SelectedItem = EliteCommandBrowser.AssignmentStatuses.Single(x => x.Status == GameCommandAssignmentStatus.All); _assignmentsView.CommandCategoryPicker.SelectedIndex = 0; _assignmentsView.CommandTypePicker.SelectedItem = EliteCommandBrowser.InputTypes.Single(x => x.Type is null); _assignmentsView.CommandSearch.Clear(); _refreshing = false;
            FilterCommands(selected);
        }

        #endregion END private helpers
    }
}


