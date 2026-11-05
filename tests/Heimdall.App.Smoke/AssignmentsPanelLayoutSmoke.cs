using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that a tall native inspector scrolls before it takes the controller viewport's reserved space.</summary>
    internal static class AssignmentsPanelLayoutSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var view = new AssignmentsBaseProbe();
            var header = new TextBlock { Text = "Native controller scope and layer selection with zoom controls, connection status and physical inputs for the selected device", TextWrapping = TextWrapping.Wrap, MinHeight = 48 };
            var drawing = new Border();
            var controller = new Grid();
            controller.RowDefinitions.Add(new() { Height = GridLength.Auto });
            controller.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(drawing, 1); controller.Children.Add(header); controller.Children.Add(drawing);
            var lastField = new Button { Content = "Last native inspector field", VerticalAlignment = VerticalAlignment.Bottom };
            var inspector = new Grid { Height = 260 }; inspector.Children.Add(lastField);
            view.ControllerPanel = controller; view.EditorPanel = inspector;
            header.SizeChanged += (_, _) => view.ControllerReserveHeight = header.ActualHeight + 160;
            var window = new Window { Content = view, Width = 900, Height = 640 };
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window);
                foreach (var fontSize in new[] { 12d, 18d })
                {
                    view.FontSize = fontSize;
                    window.Width = 700; window.Height = 360; ThemeControlSmoke.Pump(window);
                    var scroll = InspectorViewport(inspector);
                    var drawingBounds = new Rect(drawing.TranslatePoint(new Point(), view), drawing.RenderSize);
                    var inspectorBounds = new Rect(scroll.TranslatePoint(new Point(), view), scroll.RenderSize);
                    if (drawing.ActualHeight < 150 || scroll.ScrollableHeight <= 0 ||
                        drawingBounds.Bottom > inspectorBounds.Top + .5 || inspectorBounds.Bottom > view.ActualHeight + .5)
                        throw new InvalidOperationException($"Compact assignment allocation failed: drawing={drawing.ActualHeight:F1}, inspector={scroll.ActualHeight:F1}, scrollable={scroll.ScrollableHeight:F1}.");
                    scroll.ScrollToEnd(); ThemeControlSmoke.Pump(window);
                    var fieldBounds = new Rect(lastField.TranslatePoint(new Point(), scroll), lastField.RenderSize);
                    if (scroll.VerticalOffset <= 0 || fieldBounds.Top < -.5 || fieldBounds.Bottom > scroll.ActualHeight + .5)
                        throw new InvalidOperationException("The bounded inspector made its last native field unreachable.");
                    window.Width = 900; window.Height = 640; ThemeControlSmoke.Pump(window);
                    if (scroll.ScrollableHeight > .5 || drawing.ActualHeight < 150)
                        throw new InvalidOperationException("The inspector did not regain natural height after enlarging the assignment view.");
                }
            }
            finally { window.Close(); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ScrollViewer InspectorViewport(DependencyObject content)
        {
            for (var parent = VisualTreeHelper.GetParent(content); parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is ScrollViewer scroll) return scroll;
            throw new InvalidOperationException("The composed inspector has no scrolling viewport.");
        }
        #endregion END private helpers
    }
}
