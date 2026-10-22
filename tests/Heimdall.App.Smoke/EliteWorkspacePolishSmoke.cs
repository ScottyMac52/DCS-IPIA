using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks real command status filters, immediate assignment/test states, uniform actions and remembered direct exports.</summary>
    public static class EliteWorkspacePolishSmoke
    {
        #region BEGIN Public
        /// <summary>Uses the existing linked synthetic ED window and input source without touching actual game files.</summary>
        public static void Verify(EliteWorkspaceWindow window, string root)
        {
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            var status = Box<ComboBox>(window, "AssignmentStatusPicker");
            var commands = Box<ListBox>(window, "CommandsList"); var input = Box<ComboBox>(window, "InputKey");
            var actions = Box<FeatureActionBar>(window, "FeatureActions");
            var frozen = JsonSerializer.Serialize(window.Session!.Snapshot);
            var rows = EliteWorkspaceProjection.Rows(window.Session.Snapshot.Profile, window.Session.Catalog);
            var selected = ((GameDeviceChoice)Box<ListBox>(window, "DevicePicker").SelectedItem).Id;
            for (var filter = 0; filter < 5; filter++)
            {
                status.SelectedIndex = filter;
                var expected = EliteCommandBrowser.Filter(window.Session.Catalog.Commands, assignments: rows, deviceId: selected, status: (EliteAssignmentStatus)filter);
                if (!commands.Items.Cast<SimCommand>().Select(x => x.Id).SequenceEqual(expected.Select(x => x.Id)))
                    throw new InvalidOperationException("Assignment status did not intersect the command catalog correctly.");
            }
            Click(window, "ClearCommandFiltersButton");
            if (status.SelectedIndex != 0 || JsonSerializer.Serialize(window.Session.Snapshot) != frozen)
                throw new InvalidOperationException("Clear filters failed to reset ownership or filtering changed native state.");
            commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Primary Fire");
            AssertActions(window, false, true, true, false);
            input.Text = "Joy_27"; AssertActions(window, true, true, true, false);
            input.Text = " "; AssertActions(window, false, true, true, false);
            input.Text = "Joy_27";
            Action(window, "StartInputButton"); AssertActions(window, false, false, false, true);
            // Editing is allowed during sampling, but applying and clearing must wait for Stop.
            input.Text = "Joy_28"; AssertActions(window, false, false, false, true);
            Action(window, "StopInputButton"); AssertActions(window, true, true, true, false);
            Action(window, "ApplyAssignmentButton"); AssertActions(window, false, true, true, false);
            input.Text = ""; Action(window, "ClearAssignmentButton"); AssertActions(window, false, false, true, false);
            window.Session.Discard();
            commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Pitch Axis Raw");
            Box<TextBox>(window, "AxisDeadzone").Text = "invalid"; AssertActions(window, false, true, true, false);
            window.Session.Discard();
            ThemeControlSmoke.Pump(window);
            var buttons = ThemeControlSmoke.Children(actions).OfType<Button>().Where(x => x.IsVisible).ToArray();
            if (buttons.Length != actions.Actions.Length || buttons.Any(x => x.ActualWidth != 176 || x.ActualHeight != 30)
                || buttons.Any(x => !ReferenceEquals(x.Style, window.FindResource("ToolbarActionButton"))))
                throw new InvalidOperationException("Visible action buttons lost their uniform shared style or dimensions.");
            var directory = Path.Combine(root, "DirectBindings"); Directory.CreateDirectory(directory);
            var destination = Box<TextBox>(window, "BindingsDirectoryText"); var automatic = destination.Text;
            window.BindingsDirectoryPicker = () => directory;
            Click(window, "BrowseBindingsDirectoryButton");
            if (destination.Text != directory) throw new InvalidOperationException("Browse did not select the bindings destination.");
            Click(window, "ResetBindingsDirectoryButton");
            if (destination.Text != automatic) throw new InvalidOperationException("Reset did not restore automatic ED directory detection.");
            window.SetBindingsDirectory(directory); Box<CheckBox>(window, "ExportToReviewFolder").IsChecked = false;
            if (!window.SaveProfile()) throw new InvalidOperationException("Direct export fixture did not save.");
            Click(window, "ExportPresetButton");
            var target = Directory.GetFiles(directory, "*.binds").Single(); var before = File.ReadAllBytes(target);
            window.ConfirmPresetReplacement = _ => false; Click(window, "ExportPresetButton");
            if (Directory.GetFiles(directory, "*.backup-*").Length != 0) throw new InvalidOperationException("Cancelled replacement created output.");
            window.ConfirmPresetReplacement = _ => true; Click(window, "ExportPresetButton");
            if (!before.SequenceEqual(File.ReadAllBytes(Directory.GetFiles(directory, "*.backup-*").Single()))
                || Directory.GetFiles(directory, "*.start").Length != 0 || !Equals(Box<Button>(window, "ExportPresetButton").Content, "Export"))
                throw new InvalidOperationException("Direct export lost exact backup bytes, selector isolation or its simplified label.");
            // Return the existing smoke fixture to its separate-review mode and original command selection.
            Box<CheckBox>(window, "ExportToReviewFolder").IsChecked = true;
            commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Pitch Axis Raw");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void AssertActions(EliteWorkspaceWindow window, bool apply, bool clear, bool start, bool stop)
        {
            var expected = new[] { apply, clear, start, stop }; var names = new[] { "ApplyAssignmentButton", "ClearAssignmentButton", "StartInputButton", "StopInputButton" };
            var actions = Box<FeatureActionBar>(window, "FeatureActions").Actions;
            for (var i = 0; i < names.Length; i++)
                if (Box<Button>(window, names[i]).IsEnabled != expected[i] || actions.Single(x => x.Id == names[i]).Command.CanExecute(null) != expected[i])
                    throw new InvalidOperationException(names[i] + " did not update its source and shared action state.");
        }
        private static void Action(EliteWorkspaceWindow window, string name) => Box<FeatureActionBar>(window, "FeatureActions").Actions.Single(x => x.Id == name).Command.Execute(null);
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
