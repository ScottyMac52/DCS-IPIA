using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{

    /// <summary>Exercises compact navigation, viewport behavior, and restored presentation state.</summary>
    public static class CompactWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Checks actual navigation, split layout, Fit, manual zoom and preference restoration.</summary>
        public static void Verify()
        {
            WorkspaceLayoutStore.Write("assignments.inspector", 0);
            WorkspaceLayoutStore.Write("assignments.zoom", 0);
            WorkspaceLayoutStore.Write("assignments.split", .3);
            var workspace = new EditorWorkspaceSnapshot(new(new DcsModule(Guid.NewGuid(), "COMPACT", "Compact layout"),
                [], [], [], "smoke"), [], [], [], [], [], [], new Dictionary<string, string>(),
                new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
            var original = EditorSnapshotSerializer.Serialize(workspace);
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), deviceDiscovery: new EmptyDeviceDiscovery());
            try
            {
                window.Width = 1060;
                window.Height = 680;
                window.Show();
                Pump(window);
                var nav = (ListBox)window.FindName("FeatureNav")!;
                var devices = (ListBoxItem)nav.Items[0];
                var modifiers = (ListBoxItem)nav.Items[1];
                if (modifiers.TranslatePoint(new Point(), window).X <= devices.TranslatePoint(new Point(), window).X)
                    throw new InvalidOperationException("Feature navigation is not horizontal.");
                foreach (var item in nav.Items.Cast<ListBoxItem>())
                {
                    nav.SelectedItem = item;
                    Pump(window);
                    if (((ContentControl)window.FindName("FeatureContent")!).Content is not UserControl)
                        throw new InvalidOperationException("Feature did not load: " + item.Tag);
                }
                var view = WorkspaceUi.Navigate(window, "Assignments");
                Pump(window);
                var commands = (UserControl)view.FindName("CommandManager")!;
                var viewport = (ScrollViewer)view.FindName("DiagramScroll")!;
                if (commands.TranslatePoint(new Point(), window).X >= viewport.TranslatePoint(new Point(), window).X ||
                    viewport.ActualHeight < 150)
                    throw new InvalidOperationException("Assignments did not reserve usable controller space on the right.");

                // A large drawing stands in for full controller artwork, including the callout extents.
                var drawing = (Canvas)view.FindName("Diagram")!;
                drawing.Width = 1800;
                drawing.Height = 1400;
                drawing.Children.Add(new Border { Width = 1800, Height = 1400, Background = Brushes.Gray });
                Click((Button)view.FindName("FitDiagramButton")!);
                Pump(window);
                var zoom = (Slider)view.FindName("Zoom")!;
                if (drawing.Width * zoom.Value > viewport.ActualWidth || drawing.Height * zoom.Value > viewport.ActualHeight)
                    throw new InvalidOperationException("Fit clipped the complete controller bounds.");
                zoom.Value = 1.25;
                window.Width -= 100;
                Pump(window);
                if (Math.Abs(zoom.Value - 1.25) > .001 || viewport.ScrollableWidth <= 0)
                    throw new InvalidOperationException("Resize reset manual zoom or removed panning.");
                var details = (Expander)view.FindName("InputDetails")!;
                details.IsExpanded = true;
                Pump(window);
                if (WorkspaceLayoutStore.Read("assignments.inspector", 0, 0, 1) != 1 ||
                    WorkspaceLayoutStore.Read("assignments.zoom", 0, 0, 5) != 1.25)
                    throw new InvalidOperationException("Presentation preferences were not persisted.");
                WorkspaceUi.Navigate(window, "Devices");
                WorkspaceUi.Navigate(window, "Assignments");
                Pump(window);
                if (!ReferenceEquals(view, ((ContentControl)window.FindName("FeatureContent")!).Content))
                    throw new InvalidOperationException("Navigation lost the cached view.");
                var output = (ToggleButton)window.FindName("OutputMenuButton")!;
                output.IsChecked = true;
                Pump(window);
                if (!((Button)window.FindName("ValidateModuleButton")!).IsVisible)
                    throw new InvalidOperationException("Output actions are unreachable from the compact menu.");
                output.IsChecked = false;
                if (EditorSnapshotSerializer.Serialize(workspace) != original)
                    throw new InvalidOperationException("Layout changed module data.");
            }
            finally { window.Close(); }

            // A new window restores safe proportions rather than off-screen pixel widths.
            WorkspaceLayoutStore.Write("assignments.split", 9999);
            var reopened = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), deviceDiscovery: new EmptyDeviceDiscovery());
            try
            {
                reopened.Width = 1024;
                reopened.Height = 640;
                reopened.Show();
                var view = WorkspaceUi.Navigate(reopened, "Assignments");
                Pump(reopened);
                var grid = (Grid)view.FindName("AssignmentWorkspace")!;
                if (grid.ColumnDefinitions[0].Width.Value > .7 || grid.ColumnDefinitions[2].ActualWidth < 299)
                    throw new InvalidOperationException("Restored split geometry escaped its safe bounds.");
                if (!((Expander)view.FindName("InputDetails")!).IsExpanded || Math.Abs(((Slider)view.FindName("Zoom")!).Value - 1.25) > .001)
                    throw new InvalidOperationException("A reopened workspace did not restore view preferences.");
            }
            finally { reopened.Close(); }
            WorkspaceLayoutStore.Write("assignments.inspector", 0);
            WorkspaceLayoutStore.Write("assignments.zoom", 0);
            WorkspaceLayoutStore.Write("assignments.split", .3);
        }

        #endregion END Public

        #region BEGIN private helpers
        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static void Pump(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }
        #endregion END private helpers
    }
}
