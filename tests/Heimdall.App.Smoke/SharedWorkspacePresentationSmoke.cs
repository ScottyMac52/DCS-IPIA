using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Features.Assignments;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Prevents simulator consumers from replacing common presentation with independent feature layouts.</summary>
    internal static class SharedWorkspacePresentationSmoke
    {
        #region BEGIN Public
        /// <summary>Checks all four assignment consumers and the game drawing/navigation components.</summary>
        internal static void Verify()
        {
            UserControl[] consumers = [new AssignmentsView(), new EliteAssignmentsView(), new SquadronsAssignmentsView(), new WarThunderAssignmentsView()];
            foreach (var consumer in consumers)
            {
                var split = Descendants(consumer).OfType<GameWorkspaceSplit>().Single();
                if (split.ColumnDefinitions.Count != 3 || split.ColumnDefinitions[0].MinWidth != 230
                    || split.ColumnDefinitions[2].MinWidth != 300 || split.Children.OfType<GridSplitter>().Count() != 1)
                    throw new InvalidOperationException(consumer.GetType().Name + " bypassed the common assignment split.");
                var editor = Descendants(consumer).OfType<GameAssignmentEditorView>().Single();
                if (editor.NativeEditor is null) throw new InvalidOperationException("An adapter lost its native editor slot.");
            }
            foreach (var consumer in consumers.Skip(1))
            {
                var drawing = Descendants(consumer).OfType<GameDrawingView>().Single();
                if (!ReferenceEquals(drawing.Viewport.Content, drawing.Surface) || drawing.Zoom.Width != 140
                    || drawing.Viewport.CanContentScroll || !drawing.Activity.IsReadOnly)
                    throw new InvalidOperationException("A game drawing lost the shared viewport or raw-input fallback contract.");
            }
            var warThunder = (WarThunderAssignmentsView)consumers[3];
            if (warThunder.CapturedAssignments.IsExpanded || warThunder.Editor.Frame.NativeEditor is null)
                throw new InvalidOperationException("Native diagnostics displaced the standard drawing/editor workflow.");
            var content = new ContentControl(); var actions = new FeatureActionBar();
            var host = new GameWorkspaceFeatureHost(content, actions); host.Show(warThunder);
            if (actions.Actions.FirstOrDefault()?.Id != "Apply" || !actions.Actions.Any(action => action.Id == "Start"))
                throw new InvalidOperationException("Native assignment and live-input actions did not use the shared feature bar.");
            host.Clear();
            var navigation = new GameFeatureNavigation(); navigation.Items.Add(new ListBoxItem { Content = "Devices" });
            navigation.Items.Add(new ListBoxItem { Content = "Assignments & Testing" }); navigation.SelectedIndex = 1;
            if (navigation.SelectedIndex != 1 || ScrollViewer.GetHorizontalScrollBarVisibility(navigation) != ScrollBarVisibility.Disabled)
                throw new InvalidOperationException("Shared feature tabs lost selection or scrolling behavior.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var descendant in Descendants(child)) yield return descendant;
        }
        #endregion END private helpers
    }
}
