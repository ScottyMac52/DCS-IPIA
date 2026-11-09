using System.Windows;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Binds the inherited native browser projection to the War Thunder WPF panels.</summary>
    internal sealed class WarThunderAssignmentBrowser(WarThunderWorkspaceContext context,
        Action refreshEditor, WarThunderAssignmentsView assignments)
        : Heimdall.WarThunder.WarThunderAssignmentBrowser(context, context.Backend)
    {
        internal void RefreshCommands()
        {
            var browser = assignments.Browser;
            Query(new(context.SelectedDevice,
                browser.CommandCategoryPicker.SelectedIndex > 0 ? browser.CommandCategoryPicker.SelectedItem as string : null,
                (browser.CommandTypePicker.SelectedItem as GameCommandTypeOption)?.Type,
                (browser.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Status ?? GameCommandAssignmentStatus.All), browser.CommandSearch.Text);
            Publish();
        }
        internal void RefreshRecords()
        {
            SelectCommand((assignments.Browser.CommandsList.SelectedItem as SimCommand)?.Id); Publish();
        }
        protected override void OnProjectionChanged() => Publish();
        private (string?, string?, int) _editorKey;
        private void Publish()
        {
            var previous = context.Refreshing; context.Refreshing = true;
            try
            {
                context.Rows = AllRecords;
                var browser = assignments.Browser;
                if (!ReferenceEquals(browser.CommandsList.ItemsSource, Commands)) browser.CommandsList.ItemsSource = Commands;
                browser.CommandsList.SelectedItem = Commands.FirstOrDefault(x => x.Id == context.State.Selection.CommandId);
                browser.CommandCount.Text = Commands.Count + " captured commands";
                browser.CommandEmptyHint.Visibility = Commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (!ReferenceEquals(assignments.Records.ItemsSource, Records)) assignments.Records.ItemsSource = Records;
                assignments.Records.SelectedItem = Records.FirstOrDefault(x => x.RecordId == context.State.Selection.AlternativeId);
                assignments.AssignmentCount.Text = AllRecords.Count(x => x.DeviceIds.Count > 0 && (context.SelectedDevice is null || x.DeviceIds.Contains(context.SelectedDevice)))
                    + " captured joystick assignments · " + AllRecords.Count + " native records";
            }
            finally { context.Refreshing = previous; }
            var key = (context.State.Selection.CommandId, context.State.Selection.AlternativeId, Metrics.ContentBuilds);
            if (_editorKey != key) { _editorKey = key; refreshEditor(); }
        }
    }
}
