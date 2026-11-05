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
        Action UpdateAvailability,
        Action<string> Status) : IDisposable
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
            if (_session is null) return;
            _selection.SelectScope(_session.Snapshot.Id.ToString("N") + ":" + Section);
            var previousCategory = CommandCategory;
            var categories = EliteCommandBrowser.Categories(_commands, Section).Prepend("All categories").ToArray();
            _refreshing = true;
            _assignmentsView.CommandCategoryPicker.ItemsSource = categories;
            _assignmentsView.CommandCategoryPicker.SelectedItem = categories.Contains(previousCategory) ? previousCategory : categories[0];
            var query = new GameCommandQuery(Section == "All" ? null : Section, CommandCategory, InputType,
                _assignmentsView.CommandSearch.Text,
                (_assignmentsView.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Status ?? GameCommandAssignmentStatus.All);
            var commands = GameCommandBrowser.Filter(EliteCommandBrowser.Entries(_commands, _rows), query, Device?.Id)
                .Select(x => x.Command).ToArray();
            _selection.Refresh(Device?.Id, commands, selected);
            var view = new ListCollectionView(commands.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription("Metadata[ed.category]"));
            _assignmentsView.CommandsList.ItemsSource = view;
            _assignmentsView.CommandsList.SelectedItem = commands.FirstOrDefault(x => x.Id == _selection.CommandId); _refreshing = false;
            _assignmentsView.CommandCount.Text = commands.Length + " of " + _commands.Count + " commands";
            _assignmentsView.CommandEmptyHint.Visibility = commands.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.AssignmentsGrid.ItemsSource = _rows.Where(x => x.DeviceId == Device?.Id && (Section == "All" || x.Section == Section)).ToArray();
            _assignmentsView.RefreshNativeAssignments(); UpdateAvailability();
        }

        internal void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }

        internal void CommandFilterChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }

        internal void ClearCommandFiltersClicked(object sender, RoutedEventArgs e) => ClearCommandFilters(Command?.Id);

        internal void CommandChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            _selection.SelectCommand((_assignmentsView.CommandsList.SelectedItem as SimCommand)?.Id);
            _assignmentsView.RefreshNativeAssignments();
        }

        internal void SlotChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) _assignmentsView.RefreshNativeAssignments(); }

        internal void AssignmentSelected(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _assignmentsView.AssignmentsGrid.SelectedItem is EliteAssignmentRow row) SelectRow(row); }

        internal void SelectRow(EliteAssignmentRow row)
        {
            if (!_assignmentsView.CommandsList.Items.Cast<SimCommand>().Any(x => x.Id == row.CommandId))
            {
                ClearCommandFilters(row.CommandId);
                Status("Command filters cleared to show the selected assignment.");
            }
            _selection.SelectCommand(row.CommandId);
            _refreshing = true; _assignmentsView.CommandsList.SelectedItem = _assignmentsView.CommandsList.Items.Cast<SimCommand>().FirstOrDefault(x => x.Id == row.CommandId); _assignmentsView.SlotPicker.SelectedIndex = row.Slot == "Secondary" ? 1 : 0; _refreshing = false;
            _assignmentsView.RefreshNativeAssignments();
        }
        /// <summary>Detaches native command drag handlers.</summary>
        public void Dispose() => _commandDrag.Dispose();
        #endregion END Public
        #region BEGIN private helpers
        private void ClearCommandFilters(string? selected)
        {
            _refreshing = true; _assignmentsView.AssignmentStatusPicker.SelectedItem = EliteCommandBrowser.AssignmentStatuses.Single(x => x.Status == GameCommandAssignmentStatus.All); _assignmentsView.CommandCategoryPicker.SelectedIndex = 0; _assignmentsView.CommandTypePicker.SelectedItem = EliteCommandBrowser.InputTypes.Single(x => x.Type is null); _assignmentsView.CommandSearch.Clear(); _refreshing = false;
            FilterCommands(selected);
        }

        #endregion END private helpers
    }
}
