using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.Dcs;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises shared workspace geometry and presentation-only selection through actual WPF controls.</summary>
    public static class UnifiedWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Checks DCS rail selection across Devices, Assign and Test without changing saved/native state.</summary>
        public static void VerifyDcs()
        {
            var module = new DcsModule(Guid.NewGuid(), "UNIFIED", "Unified workspace");
            var profiles = new[] { Profile("Left"), Profile("Right") };
            var diagrams = profiles.Select(x => new UiLayerDiagram(x.Role!, null, "controller.svg", 240, 120, [], [])
            {
                ProfileId = x.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 240 120'><rect x='20' y='20' width='200' height='80' fill='#263b58'/></svg>"
            }).ToArray();
            var workspace = new EditorWorkspaceSnapshot(new(module, profiles, [], [], "smoke"), [], [], diagrams, [], [], [],
                new Dictionary<string, string>(), new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
            var original = EditorSnapshotSerializer.Serialize(workspace);
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), deviceDiscovery: new EmptyDeviceDiscovery(), persistLayout: false);
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window);
                var rail = Box<ListBox>(window, "ControllerPicker");
                rail.SelectedIndex = 1;
                foreach (var (feature, selector) in new[] { ("Assignments", "DeviceChoice"), ("InputTest", "FeedbackDevice"), ("Devices", "ManagedDevices") })
                {
                    var view = WorkspaceUi.Navigate(window, feature); ThemeControlSmoke.Pump(window);
                    if (!Equals(Box<Selector>(view, selector).SelectedValue, profiles[1].Id) || !Equals(rail.SelectedValue, profiles[1].Id))
                        throw new InvalidOperationException("DCS navigation lost the selected controller: " + feature);
                    VerifyDivider(window, Box<Grid>(window, "ControllerPanes"));
                }
                var assignments = WorkspaceUi.Navigate(window, "Assignments"); ThemeControlSmoke.Pump(window);
                VerifyDivider(window, Box<Grid>(assignments, "AssignmentWorkspace"));
                Box<RadioButton>(window, "TestMode").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                if (Box<ContentControl>(window, "FeatureContent").Content is not Heimdall.App.Features.InputTest.InputTestView)
                    throw new InvalidOperationException("Test inputs did not open the native DCS test feature.");
                Box<RadioButton>(window, "AssignMode").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                if (!ReferenceEquals(assignments, Box<ContentControl>(window, "FeatureContent").Content))
                    throw new InvalidOperationException("Assign/Test mode discarded the cached assignment surface.");
                ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "unified-dcs-assignments");
                if (EditorSnapshotSerializer.Serialize(window._working!) != original ||
                    !window._draft!.Snapshot.SequenceEqual(workspace.Assignments) || window._labels.Count != workspace.Labels.Count)
                    throw new InvalidOperationException("Presentation geometry or selection changed the DCS snapshot.");
                var added = Profile("New");
                window.RunStructureEdit(current => current with
                {
                    Configuration = new(module, profiles.Append(added), [], [], "smoke"),
                    Diagrams = diagrams.Append(diagrams[0] with { ProfileId = added.Id, Device = "New" }).ToArray()
                });
                var test = WorkspaceUi.Navigate(window, "InputTest"); ThemeControlSmoke.Pump(window);
                rail.SelectedValue = added.Id;
                if (rail.Items.Count != 3 || !Equals(Box<Selector>(test, "FeedbackDevice").SelectedValue, added.Id))
                    throw new InvalidOperationException("Cached input testing omitted a newly added DCS controller.");
                window.DiscardClicked(window, new RoutedEventArgs());
                WorkspaceUi.Navigate(window, "Devices"); WorkspaceUi.Navigate(window, "InputTest"); ThemeControlSmoke.Pump(window);
                if (rail.Items.Count != 2 || EditorSnapshotSerializer.Serialize(window._working!) != original)
                    throw new InvalidOperationException("Cached input testing retained a discarded controller or changed native state.");
            }
            finally { window.Close(); }
        }

        /// <summary>Checks ED column order, independently reachable inspector, all task splitters and text scaling.</summary>
        public static void VerifyElite(EliteWorkspaceWindow window)
        {
            var original = JsonSerializer.Serialize(window.Session!.Export().Files);
            var feature = Box<ListBox>(window, "FeaturePicker");
            var width = window.Width; var height = window.Height;
            var root = Box<Grid>(window, "WorkspaceRoot");
            var persistence = WorkspaceLayout.GetPersistenceEnabled(root);
            WorkspaceLayout.SetPersistenceEnabled(root, false);
            try
            {
                feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                var commands = Box<ListBox>(window, "CommandsList");
                var editor = Box<StackPanel>(window, "AssignmentEditor");
                var drawing = Box<ScrollViewer>(window, "DiagramScroll");
                if (commands.TranslatePoint(new Point(), window).X >= drawing.TranslatePoint(new Point(), window).X ||
                    editor.TranslatePoint(new Point(), window).Y < drawing.TranslatePoint(new Point(), window).Y + drawing.ActualHeight - 1)
                    throw new InvalidOperationException("ED commands and selected-input inspector did not occupy their shared workspace positions.");
                if (drawing.ActualHeight < 120 || editor.ActualWidth < 280)
                    throw new InvalidOperationException("ED diagram or selected-input editor has insufficient working space.");
                VerifyDivider(window, Box<Grid>(window, "ControllerPanes"));
                VerifyDivider(window, Box<Grid>(window, "AuthoringPanel"));
                ThemeControlSmoke.SaveImage(window, "unified-elite-assignments");
                foreach (var index in new[] { 0, 2 })
                {
                    feature.SelectedIndex = index; ThemeControlSmoke.Pump(window);
                    var content = Box<ContentControl>(window, "FeatureContent").Content as UserControl ?? throw new InvalidOperationException("Missing feature.");
                    var split = LogicalTreeHelper.GetChildren(content).OfType<DockPanel>().Single().Children.OfType<Grid>().Single();
                    VerifyDivider(window, split);
                    ThemeControlSmoke.SaveImage(window, index == 0 ? "unified-elite-devices" : "unified-elite-references");
                }
                feature.SelectedIndex = 1;
                window.Width = 1100; window.Height = 760; ThemeControlSmoke.Pump(window);
                if (drawing.ActualHeight < 120 || Box<ComboBox>(window, "InputKey").ActualWidth < 200)
                    throw new InvalidOperationException("Resizing removed usable ED drawing or assignment space.");
                window.Width = width; window.Height = height;
                root.LayoutTransform = new ScaleTransform(1.25, 1.25); ThemeControlSmoke.Pump(window);
                ThemeControlSmoke.SaveImage(window, "unified-elite-scaled");
                if (drawing.ActualHeight < 120 || !InspectorInputVisible(window))
                    throw new InvalidOperationException($"Enlarged UI scaling hid the ED input editor or drawing: drawing={drawing.ActualHeight}, input inside inspector={InspectorInputVisible(window)}.");
                if (JsonSerializer.Serialize(window.Session.Export().Files) != original)
                    throw new InvalidOperationException("Workspace presentation changed native ED bindings.");
            }
            finally { WorkspaceLayout.SetPersistenceEnabled(root, persistence); root.LayoutTransform = Transform.Identity; window.Width = width; window.Height = height; feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window); }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static bool InspectorInputVisible(Window window)
        {
            var input = Box<ComboBox>(window, "InputKey");
            var viewport = Box<ScrollViewer>(window, "AssignmentInspectorScroll");
            var available = viewport.TransformToAncestor(window).TransformBounds(new Rect(viewport.RenderSize));
            var bounds = input.TransformToAncestor(window).TransformBounds(new Rect(input.RenderSize));
            available.Inflate(1, 1);
            return input.IsVisible && available.Contains(bounds);
        }

        private static DeviceProfile Profile(string name) => new(Guid.NewGuid(), "tm-tpr", name, name, name + ".diff.lua");
        private static T Box<T>(FrameworkElement view, string name) where T : FrameworkElement => (T)view.FindName(name);
        private static void VerifyDivider(Window window, Grid grid)
        {
            var divider = grid.Children.OfType<GridSplitter>().Single();
            if (!divider.Focusable || divider.KeyboardIncrement != 16 || divider.ResizeBehavior != GridResizeBehavior.PreviousAndNext)
                throw new InvalidOperationException("A workspace boundary lost its shared accessible divider contract.");
            var before = grid.ColumnDefinitions[0].ActualWidth;
            Keyboard.Focus(divider); ThemeControlSmoke.Press(divider, Key.Right); ThemeControlSmoke.Pump(window);
            if (grid.ColumnDefinitions[0].ActualWidth <= before + 1)
                throw new InvalidOperationException("A workspace divider did not resize its adjacent panes with the keyboard.");
            ThemeControlSmoke.Press(divider, Key.Left); ThemeControlSmoke.Pump(window);
        }
        #endregion END private helpers
    }
}
