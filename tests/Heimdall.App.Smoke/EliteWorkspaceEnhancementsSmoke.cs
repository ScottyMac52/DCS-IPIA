using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Smoke
{
    /// <summary>Operates actual feature actions, controller ordering and combination editing without changing native output.</summary>
    public static class EliteWorkspaceEnhancementsSmoke
    {
        #region BEGIN Public
        /// <summary>Checks saved order, drop isolation, navigation and native combination preservation on the real window.</summary>
        public static void Verify(EliteWorkspaceWindow window, GameWorkspaceServices services)
        {
            var feature = Box<ListBox>(window, "FeaturePicker");
            var actions = Box<FeatureActionBar>(window, "FeatureActions");
            var content = Box<ContentControl>(window, "FeatureContent");
            var devices = Box<ListBox>(window, "DevicePicker");
            if (feature.Items.Count != 3 || content.Content is not EliteAssignmentsView
                || !actions.Actions.Any(x => x.Id == "ApplyAssignmentButton") || !actions.Actions.Any(x => x.Id == "StartInputButton"))
                throw new InvalidOperationException("Assignments and input testing did not share a view and action bar.");
            var session = window.Session!; var original = GameControllerOrder.For(session.Snapshot).ToArray();
            var selected = ((GameDeviceChoice)devices.SelectedItem).Id;
            var native = JsonSerializer.Serialize(session.Export().Files);
            var payload = window.ControllerDrag(original[^1]);
            if (window.DropController(new DataObject("heimdall.elite.command", "PrimaryFire"), original[0], false)
                || window.DropController(new DataObject("heimdall.elite.controller.foreign", original[^1]), original[0], false)
                || window.DropController(payload, original[^1], false)
                || window.DropController(payload, "missing", false)
                || window.DropController(window.ControllerDrag("missing"), original[0], false))
                throw new InvalidOperationException("An invalid, foreign or self drop changed controller order.");
            if (!window.DropController(payload, original[0], false)
                || ((GameDeviceChoice)devices.Items[0]).Id != original[^1] || ((GameDeviceChoice)devices.SelectedItem).Id != selected)
                throw new InvalidOperationException("Controller drop lost the selected reference or did not reorder the list.");
            if (JsonSerializer.Serialize(session.Export().Files) != native || !window.SaveProfile())
                throw new InvalidOperationException("Controller reordering changed native output or failed to save.");
            var saved = services.Store.Load(window.Session!.Snapshot.Id).Workspace;
            if (!GameControllerOrder.For(saved).SequenceEqual(devices.Items.Cast<GameDeviceChoice>().Select(x => x.Id)))
                throw new InvalidOperationException("Controller display order was not persisted independently.");
            devices.SelectedIndex = 0;
            if (Box<Button>(window, "MoveControllerUpButton").IsEnabled) throw new InvalidOperationException("First controller offered Move up.");
            Click(window, "MoveControllerUpButton");
            Click(window, "MoveControllerDownButton");
            if (devices.SelectedIndex != 1) throw new InvalidOperationException("Accessible move lost selection or order.");
            Click(window, "MoveControllerUpButton");
            if (devices.SelectedIndex != 0) throw new InvalidOperationException("Move up did not use the same order boundary.");
            devices.SelectedIndex = devices.Items.Count - 1;
            if (Box<Button>(window, "MoveControllerDownButton").IsEnabled) throw new InvalidOperationException("Last controller offered Move down.");
            window.Session!.Discard();
            window.DropController(window.ControllerDrag(original[^1]), original[^2], after: true);
            devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(x => x.Id == selected);
            if (!window.SaveProfile()) throw new InvalidOperationException("Restoring display order failed to save.");
            feature.SelectedIndex = 0; ThemeControlSmoke.Pump(window);
            if (content.Content is not EliteDevicesView || actions.Actions.Any(x => x.Id == "StartInputButton")
                || !actions.Actions.Any(x => x.Id == "IdentifyControllerButton"))
                throw new InvalidOperationException("Devices retained another feature's actions.");
            feature.SelectedIndex = 2; ThemeControlSmoke.Pump(window);
            if (content.Content is not EliteReferenceSheetsView || !actions.Actions.Any(x => x.Id == "ReferencePdfButton"))
                throw new InvalidOperationException("Reference Sheets did not own its view and actions.");
            feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            var combination = Box<DataGrid>(window, "AssignmentsGrid").Items.Cast<EliteAssignmentRow>().FirstOrDefault(x => x.Modifiers.Count > 0);
            if (combination is not null)
            {
                Box<DataGrid>(window, "AssignmentsGrid").SelectedItem = combination;
                if (!Box<Expander>(window, "ModifierTools").IsExpanded || Box<ListBox>(window, "ModifiersList").Items.Count != combination.Modifiers.Count)
                    throw new InvalidOperationException("An imported button combination was hidden or discarded when selected.");
                var before = JsonSerializer.Serialize(window.Session.Export().Files);
                var apply = actions.Actions.Single(x => x.Id == "ApplyAssignmentButton").Command;
                if (!apply.CanExecute(null)) throw new InvalidOperationException("The shared assignment action did not reflect editor availability.");
                apply.Execute(null);
                if (JsonSerializer.Serialize(window.Session.Export().Files) != before)
                    throw new InvalidOperationException("Applying an unchanged combination altered native binding semantics.");
            }
            if (JsonSerializer.Serialize(window.Session!.Export().Files) != native)
                throw new InvalidOperationException("Feature navigation or combination review changed native output.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
