using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Domain.MultiSim;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Microsoft.Win32;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Projects the native command catalog and captured records independently of editing and persistence.</summary>
    internal sealed class SquadronsAssignmentBrowser(
        SquadronsWorkspaceContext context,
        Action stopInput,
        Action refreshController,
        Func<bool, WorkspaceAssignmentActionState> clearAvailability,
        Action renderDrawing,
        Action<Action> run,
        ListBox DevicePicker,
        SquadronsDevicesView _devicesView,
        SquadronsAssignmentsView _assignmentsView)
        : Heimdall.Squadrons.SquadronsAssignmentBrowser(context, context.Backend, () => context.ControllerInputs), ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>
    {
        #region BEGIN Public
        void ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>.RefreshRows() => RefreshRows();
        void ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>.SelectRecord(SquadronsWorkspaceRow row) => SelectRecord(row);
        void ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>.SelectCommandConcept(string concept) => SelectCommandConcept(concept);
        #endregion END Public
        #region BEGIN private helpers
        internal void RefreshRows()
        {
            if (context.Refreshing) return;
            var review = _assignmentsView.ReviewCaptured.IsChecked == true;
            var section = _assignmentsView.SectionPicker.SelectedItem as string ?? "Flight";
            RefreshQuery();
            var rows = Records;
            context.Refreshing = true;
            try
            {
                if (!ReferenceEquals(_assignmentsView.BindingsGrid.ItemsSource, rows)) _assignmentsView.BindingsGrid.ItemsSource = rows;
                _assignmentsView.BindingsGrid.SelectedItem = rows.FirstOrDefault(x => x.RecordId == context.State.Selection.AlternativeId);
            }
            finally { context.Refreshing = false; }
            _assignmentsView.ContextColumn.Visibility = section == "All" ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.ControllerColumn.Visibility = review || (DevicePicker.SelectedItem is GameDeviceChoice { Id: "all" })
                ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.HardwareColumn.Visibility = _assignmentsView.BindingColumn.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.CountText.Text = review ? rows.Count + " captured records · " + rows.Count(x => x.HardwareStatus == "Input absent on linked device") + " inputs absent on linked hardware" :
                rows.Count + " captured joystick assignments · " + section + ". Check hardware status in advanced review; saved slot ownership is not physical identity.";
            if (context.FocusedConcept is not null)
                _assignmentsView.CountText.Text = "Command: " + context.FocusedConcept + " · " + rows.Count + " matching captured bindings. " +
                    "Unassigned commands remain selectable in the command browser.";
            _assignmentsView.EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.EmptyState.Text = context.Session is null ? "Import a Squadrons controls file to get started." : review ? "No matching captured records. Change the context, input filter or search." :
                context.ScanningInputs ? "Inspecting controller inputs…" : "No captured joystick assignments match these filters. Change the context or input filter, or enable advanced review for preserved native records.";
            if (context.FocusedConcept is not null && rows.Count == 0)
                _assignmentsView.EmptyState.Text = "This command has no assignments matching the current controller and input filters. The command is still available in the catalog.";
            if (context.InspectedControl is not null)
            {
                _assignmentsView.CountText.Text = context.InspectedControl.Label + " · " + rows.Count + " captured assignments · " + context.DrawingSection + ".";
                if (rows.Count == 0) _assignmentsView.EmptyState.Text = "No captured assignments on this control in the current context.";
            }
        }

        internal void CaptureReviewChanged(object sender, RoutedEventArgs e)
        {
            if (context.Refreshing) return;
            if (_assignmentsView.ReviewCaptured.IsChecked != true) context.InspectedControl = null;
            RefreshRows();
        }

        internal void CommandChanged(object sender, SelectionChangedEventArgs e)
        {
            if (context.Refreshing) return;
            var command = _assignmentsView.CommandBrowser.CommandsList.SelectedItem as Heimdall.Domain.MultiSim.SimCommand;
            if (context.Selection.CommandId != command?.Id)
                _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "";
            SelectCommand(command?.Id);
            context.FocusedConcept = null;
            context.InspectedControl = null;
            context.Refreshing = true;
            try
            {
                _assignmentsView.BindingsGrid.SelectedItem = null;
                _assignmentsView.TargetDevice.SelectedItem = _assignmentsView.TargetDevice.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == context.SelectedController);
            }
            finally { context.Refreshing = false; }
            RefreshRows();
            _assignmentsView.SelectionEditor.ShowCommand(command);
            _assignmentsView.ShowCommandButton.IsEnabled = command is not null;
            UpdateCommandHint(command);
        }

        private void UpdateCommandHint(Heimdall.Domain.MultiSim.SimCommand? command)
        {
            if (command is null || context.Session is null)
            {
                _assignmentsView.CommandHint.Text = "Choose a command to inspect its saved binding records.";
                return;
            }
            var plan = Plan(command.Metadata["squadrons.concept"]);
            _assignmentsView.CommandHint.Text = plan.CapturedRecords + " captured records · " + plan.EditableRecords + " editable.";
        }

        internal void ShowCommandClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (_assignmentsView.CommandBrowser.CommandsList.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command) return;
            context.InspectedControl = null;
            _assignmentsView.SectionPicker.SelectedItem = command.Category;
            _assignmentsView.ReviewCaptured.IsChecked = true;
            _assignmentsView.CapturedAssignments.IsExpanded = true;
            context.FocusedConcept = command.Metadata["squadrons.concept"];
            RefreshRows();
            var match = context.Rows.FirstOrDefault(row => row.Native.Concept == context.FocusedConcept
                && (context.SelectedController is null || row.DeviceId == context.SelectedController));
            if (match is not null)
                _assignmentsView.BindingsGrid.SelectedItem = _assignmentsView.BindingsGrid.Items.Cast<SquadronsWorkspaceRow>()
                    .FirstOrDefault(row => row.RecordId == match.RecordId);
            if (_assignmentsView.BindingsGrid.SelectedItem is not null) _assignmentsView.BindingsGrid.ScrollIntoView(_assignmentsView.BindingsGrid.SelectedItem);
            else _assignmentsView.CommandHint.Text = "No captured assignment matches this command and controller. No new native records were written.";
        });

        internal void RecordContextOpening(object sender, ContextMenuEventArgs e)
        {
            var row = _assignmentsView.BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            _assignmentsView.EditNativeRecordItem.IsEnabled = row is not null;
            _assignmentsView.ApplyNativeRecordItem.Visibility = row?.Editable == true && _assignmentsView.ApplyButton.IsEnabled
                ? Visibility.Visible : Visibility.Collapsed;
            AssignmentActionPresentation.Show(_assignmentsView.ClearNativeRecordItem, clearAvailability(row?.Native.Type == 3));
        }

        internal void RecordRightClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject element &&
                ItemsControl.ContainerFromElement(_assignmentsView.BindingsGrid, element) is DataGridRow row)
                _assignmentsView.BindingsGrid.SelectedItem = row.Item;
        }

        internal void EditSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (_assignmentsView.BindingsGrid.SelectedItem is SquadronsWorkspaceRow row)
                SelectRecord(row);
        }

        internal void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (context.Refreshing) return;
            if (ReferenceEquals(sender, DevicePicker))
            {
                stopInput();
                context.InspectedControl = null;
                _assignmentsView.DrawingDropFeedback.Text = "";
                _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = "";
                _assignmentsView.BindingsGrid.SelectedItem = null;
                _assignmentsView.TargetDevice.SelectedItem = null;
                if (DevicePicker.SelectedItem is GameDeviceChoice { Id: var id } &&
                    id.StartsWith("system.", StringComparison.Ordinal))
                {
                    _assignmentsView.SectionPicker.SelectedItem = "All";
                    _assignmentsView.TypePicker.SelectedItem = "All inputs";
                    _devicesView.ControllerSetup.IsExpanded = false;
                }
                run(refreshController);
                RefreshCommandCatalog(null);
                run(renderDrawing);
            }
            if (ReferenceEquals(sender, _assignmentsView.SectionPicker) || ReferenceEquals(sender, _assignmentsView.TypePicker))
            {
                context.FocusedConcept = null;
                context.InspectedControl = null;
            }
            RefreshRows();
        }

        internal void SearchChanged(object sender, TextChangedEventArgs e) { if (!context.Refreshing) { context.InspectedControl = null; RefreshRows(); } }

        internal void BindingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!context.Refreshing && _assignmentsView.BindingsGrid.SelectedItem is SquadronsWorkspaceRow row) SelectRecord(row);
        }

        internal void SelectRecord(SquadronsWorkspaceRow row)
        {
            SelectCommandConcept(row.Native.Concept);
            var previous = context.Refreshing;
            context.Refreshing = true;
            try
            {
                _assignmentsView.TargetDevice.SelectedItem = _assignmentsView.TargetDevice.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == row.DeviceId);
            }
            finally { context.Refreshing = previous; }
            base.SelectRecord(row.RecordId);
        }

        internal void SelectCommandConcept(string concept)
        {
            if (context.Session is null) return;
            var command = AllCommands.Single(x => x.Metadata["squadrons.concept"] == concept);
            var section = context.DrawingSection;
            var oldRefreshing = context.Refreshing;
            context.Refreshing = true;
            try
            {
                if (!_assignmentsView.CommandBrowser.CommandsList.Items.Cast<Heimdall.Domain.MultiSim.SimCommand>().Any(x => x.Id == command.Id))
                {
                    _assignmentsView.CommandBrowser.CommandCategoryPicker.SelectedIndex = _assignmentsView.CommandBrowser.CommandTypePicker.SelectedIndex = _assignmentsView.CommandBrowser.AssignmentStatusPicker.SelectedIndex = 0;
                    _assignmentsView.CommandBrowser.CommandSearch.Clear();
                    RefreshCommandCatalog(null, command.Id);
                }
                _assignmentsView.CommandBrowser.CommandsList.SelectedItem = _assignmentsView.CommandBrowser.CommandsList.Items.Cast<Heimdall.Domain.MultiSim.SimCommand>().Single(x => x.Id == command.Id);
                if (context.Selection.CommandId != command.Id) _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = "";
                SelectCommand(command.Id);
                _assignmentsView.SelectionEditor.ShowCommand(command);
                _assignmentsView.ShowCommandButton.IsEnabled = true;
                UpdateCommandHint(command);
            }
            finally { context.Refreshing = oldRefreshing; }
            if (section != context.DrawingSection) run(renderDrawing);
        }

        internal void RefreshCommandCatalog(GameWorkspace? snapshot, string? preferredCommandId = null)
        {
            RefreshQuery(preferredCommandId); Publish();
        }

        protected override void OnProjectionChanged() => Publish();
        private void RefreshQuery(string? preferred = null) => Query(new(context.AssignmentSource,
            _assignmentsView.CommandBrowser.CommandCategoryPicker.SelectedItem as string ?? "All",
            (_assignmentsView.CommandBrowser.CommandTypePicker.SelectedItem as GameCommandTypeOption)?.Label ?? "All types",
            (_assignmentsView.CommandBrowser.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Label ?? "All",
            _assignmentsView.SectionPicker.SelectedItem as string ?? "All", _assignmentsView.TypePicker.SelectedItem as string ?? "All inputs",
            _assignmentsView.Search.Text, _assignmentsView.ReviewCaptured.IsChecked == true, context.FocusedConcept,
            context.InspectedControl, context.DrawingSection), _assignmentsView.CommandBrowser.CommandSearch.Text, preferred);
        private void Publish()
        {
            var previous = context.Refreshing; context.Refreshing = true;
            try
            {
                context.Rows = AllRecords;
                var browser = _assignmentsView.CommandBrowser;
                if (!ReferenceEquals(browser.CommandsList.ItemsSource, Commands)) browser.CommandsList.ItemsSource = Commands;
                browser.CommandsList.SelectedItem = Commands.FirstOrDefault(x => x.Id == context.State.Selection.CommandId);
                if (!ReferenceEquals(_assignmentsView.BindingsGrid.ItemsSource, Records)) _assignmentsView.BindingsGrid.ItemsSource = Records;
                _assignmentsView.BindingsGrid.SelectedItem = Records.FirstOrDefault(x => x.RecordId == context.State.Selection.AlternativeId);
                browser.CommandCount.Text = Commands.Count + " of " + AllCommands.Count + " known/imported commands · assignment status includes all native input records.";
                browser.CommandEmptyHint.Visibility = Commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                var selected = browser.CommandsList.SelectedItem as SimCommand;
                _assignmentsView.SelectionEditor.ShowCommand(selected); UpdateCommandHint(selected);
                _assignmentsView.ShowCommandButton.IsEnabled = selected is not null;
            }
            finally { context.Refreshing = previous; }
            _assignmentsView.SynchronizeNativeAssignments();
        }

        internal void CommandFiltersChanged(object sender, SelectionChangedEventArgs e)
        {
            if (context.Refreshing) return;
            RefreshCommandCatalog(null);
            if (ReferenceEquals(sender, _assignmentsView.CommandBrowser.CommandCategoryPicker))
            {
                context.FocusedConcept = null;
                context.InspectedControl = null;
                RefreshRows();
                run(renderDrawing);
            }
        }

        internal void CommandSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (!context.Refreshing) { RefreshCommandCatalog(null); }
        }

        internal void ResetCommandFiltersClicked(object sender, RoutedEventArgs e)
        {
            context.Refreshing = true;
            try
            {
                _assignmentsView.CommandBrowser.CommandCategoryPicker.SelectedIndex = 0;
                _assignmentsView.CommandBrowser.CommandTypePicker.SelectedIndex = 0;
                _assignmentsView.CommandBrowser.AssignmentStatusPicker.SelectedIndex = 0;
                _assignmentsView.CommandBrowser.CommandSearch.Clear();
            }
            finally { context.Refreshing = false; }
            RefreshCommandCatalog(null);
            run(renderDrawing);
        }
        #endregion END private helpers
    }
}


