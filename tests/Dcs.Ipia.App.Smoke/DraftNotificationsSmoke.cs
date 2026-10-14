using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Features.Commands;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Verifies immediate draft propagation through the shell and both cached command managers.</summary>
    public static class DraftNotificationsSmoke
    {
        private static readonly string[] OutputButtons = ["BuildButton", "ValidateModuleButton", "ExportModuleSnapshotButton",
            "ExportModuleDocumentationButton", "ExportCompleteReleaseButton"];

        #region BEGIN Public
        /// <summary>Exercises assignment, undo, conflicts, save, discard and preserved feature selections in WPF.</summary>
        public static void Verify()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-draft-notifications-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            ModuleWorkspaceWindow? window = null;
            try
            {
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", "Test.diff.lua");
                var configuration = new ModuleConfiguration(new(Guid.NewGuid(), "NOTIFY", "Notifications"), [profile],
                    [new DcsModifier("Shift", "test", "JOY_BTN9", ModifierMode.Hold)], [], "Created in IPIA");
                var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
                {
                    ProfileId = profile.Id,
                    Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100'/></svg>",
                    Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20)]
                };
                var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [diagram], [],
                    [new CommandPaletteEntry("KEY", "Test action", "Keys", false)], [], new Dictionary<string, string>(),
                    new Dictionary<string, string> {
                        ["config/kneeboard.json"] = """{"profiles":{},"pages":[],"summaryPages":[]}""",
                        ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
                    });
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository);
                window.Show();
                ((TextBox)window.FindName("PackageVersion")).Text = "1.0.0";
                var context = new WindowContext(window);
                var commandsView = WorkspaceUi.Navigate(window, "Commands");
                var standalone = (CommandsManagementView)commandsView.FindName("Manager");
                var assignmentsView = WorkspaceUi.Navigate(window, "Assignments");
                var embedded = (CommandsManagementView)assignmentsView.FindName("CommandManager");
                var device = (ComboBox)assignmentsView.FindName("DeviceChoice");
                var layers = (ComboBox)assignmentsView.FindName("Layers");
                layers.SelectedItem = "Shift";
                ConfigureFilters(standalone);
                ConfigureFilters(embedded);
                WorkspaceUi.SelectCommand((DataGrid)embedded.FindName("Commands"), "KEY");
                WorkspaceUi.SelectCommand((DataGrid)standalone.FindName("Commands"), "KEY");
                AssertState(window, standalone, embedded, false, 0);

                var canvas = (Canvas)assignmentsView.FindName("Diagram");
                var callout = ((Canvas)canvas.Children[0]).Children.OfType<Border>().Single();
                callout.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                WorkspaceUi.Button(assignmentsView, "Assign selected command").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AssertState(window, standalone, embedded, true, 1);
                if (device.SelectedItem is not UiLayerDiagram selected || selected.ProfileId != profile.Id ||
                    !Equals(layers.SelectedItem, "Shift") || embedded.SelectedCommand?.Id != "KEY" || standalone.SelectedCommand?.Id != "KEY")
                    throw new InvalidOperationException("Draft notification reset a valid device, layer or command selection.");
                if (!ReferenceEquals(WorkspaceUi.Navigate(window, "Commands"), commandsView))
                    throw new InvalidOperationException("Commands view was not cached.");
                AssertState(window, standalone, embedded, true, 1);
                WorkspaceUi.Navigate(window, "Assignments");
                WorkspaceUi.Button(assignmentsView, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AssertState(window, standalone, embedded, false, 0);

                var slot = new DraftAssignmentSlot(configuration.Module.Id, profile.Id, AssignmentLayer.Module,
                    BindingSection.Key, "JOY_BTN1", "Shift");
                var command = new DraftCommand("KEY", "Test action", false, AssignmentLayer.Module);
                context.Draft!.Assign(slot, command, OccupiedDestinationBehavior.Ask);
                var other = new DraftCommand("OTHER", "Other action", false, AssignmentLayer.Module);
                context.Draft.AddCommands([other]);
                context.Draft.Assign(slot, other, OccupiedDestinationBehavior.Ask);
                context.Draft.Assign(slot, other, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Cancel);
                context.Draft.Assign(slot, other, OccupiedDestinationBehavior.Reject);
                AssertState(window, standalone, embedded, true, 1);
                SetStateFilter(standalone, "Unassigned on every device");
                SetStateFilter(embedded, "Unassigned on every device");
                AssertRows(standalone, 0);
                AssertRows(embedded, 0);
                context.Discard();
                AssertRows(standalone, 1);
                AssertRows(embedded, 1);
                AssertState(window, standalone, embedded, false, 0);
                SetStateFilter(standalone, "All");
                SetStateFilter(embedded, "All");
                context.Draft!.Assign(slot, command, OccupiedDestinationBehavior.Ask);
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                Task save;
                try { save = context.SaveAsync(); }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                Pump(window, save);
                AssertState(window, standalone, embedded, false, 1);
                context.Draft!.Remove(slot);
                AssertState(window, standalone, embedded, true, 0);
                context.Discard();
                AssertState(window, standalone, embedded, false, 1);
                context.Draft!.Remove(slot);
                context.Draft.Undo();
                AssertState(window, standalone, embedded, false, 1);
                window.Close();
                window = null;
                Console.WriteLine("Draft mutations synchronized the shell and both cached command managers.");
            }
            finally
            {
                if (window is not null) { new WindowContext(window).Discard(); window.Close(); }
                File.Delete(path);
            }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void ConfigureFilters(CommandsManagementView manager)
        {
            ((TextBox)manager.FindName("CommandSearch")).Text = "Test";
            ((ComboBox)manager.FindName("CommandCategoryFilter")).SelectedItem = "Keys";
            ((ComboBox)manager.FindName("CommandTypeFilter")).SelectedIndex = 1;
        }

        private static void SetStateFilter(CommandsManagementView manager, string value)
        {
            var filter = (ComboBox)manager.FindName("CommandStateFilter");
            filter.SelectedItem = filter.Items.Cast<ComboBoxItem>().Single(x => Equals(x.Content, value));
        }

        private static void AssertRows(CommandsManagementView manager, int count)
        {
            if (((DataGrid)manager.FindName("Commands")).Items.Count != count)
                throw new InvalidOperationException("Assignment-dependent command filter did not refresh.");
        }

        private static void AssertState(ModuleWorkspaceWindow window, CommandsManagementView standalone,
            CommandsManagementView embedded, bool dirty, int bound)
        {
            var summary = ((TextBlock)window.FindName("PendingSummary")).Text;
            if (summary.StartsWith("Pending", StringComparison.Ordinal) != dirty ||
                OutputButtons.Any(name => ((Button)window.FindName(name)).IsEnabled == dirty))
                throw new InvalidOperationException("Pending summary or output availability is stale: " + summary);
            foreach (var manager in new[] { standalone, embedded })
            {
                var row = ((DataGrid)manager.FindName("Commands")).Items.Cast<object>().Single();
                if (!Equals(row.GetType().GetProperty("Bound")!.GetValue(row), bound) ||
                    ((TextBox)manager.FindName("CommandSearch")).Text != "Test" ||
                    !Equals(((ComboBox)manager.FindName("CommandCategoryFilter")).SelectedItem, "Keys") ||
                    ((ComboBox)manager.FindName("CommandTypeFilter")).SelectedIndex != 1)
                    throw new InvalidOperationException("Command usage or filters are stale after draft mutation.");
            }
        }

        private static void Pump(Window window, Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Draft save timed out.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
            task.GetAwaiter().GetResult();
        }
        #endregion END private helpers
    }
}
