using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks actual wrapped button/caption bounds as available width, font size and toolbar style change.</summary>
    internal static class FeatureActionLayoutSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var source = new ActionBar { IsFeatureActions = true };
            var clicked = 0;
            for (var index = 0; index < 8; index++)
            {
                var button = new Button { Content = $"Action {index + 1}: stage selected controller assignment" };
                button.Click += (_, _) => clicked++;
                source.Children.Add(button);
            }
            var bar = new FeatureActionBar(); bar.ShowActions(source);
            var content = new Border { MinHeight = 100 };
            var layout = new Grid();
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(content, 1); layout.Children.Add(bar); layout.Children.Add(content);
            var window = new Window { Content = layout, Width = 1100, Height = 900 };
            try
            {
                window.Show();
                foreach (var customStyle in new[] { false, true })
                foreach (var fontSize in new[] { 12d, 18d })
                {
                    bar.ButtonStyle = customStyle ? (Style)System.Windows.Application.Current.FindResource("ToolbarActionButton") : null;
                    bar.FontSize = fontSize;
                    window.Width = 1100; ThemeControlSmoke.Pump(window);
                    AssertFits(bar, 8);
                    var wideHeight = bar.ActualHeight;
                    window.Width = 700; ThemeControlSmoke.Pump(window);
                    AssertFits(bar, 8);
                    window.Width = 420; ThemeControlSmoke.Pump(window);
                    var rows = AssertFits(bar, 8);
                    if (rows <= 1 || bar.ActualHeight <= wideHeight || content.TranslatePoint(new Point(), layout).Y < bar.ActualHeight - .5)
                        throw new InvalidOperationException("Narrow feature actions did not wrap/grow above the feature content.");
                    window.Width = 1100; ThemeControlSmoke.Pump(window);
                    AssertFits(bar, 8);
                    if (Math.Abs(bar.ActualHeight - wideHeight) > .5)
                        throw new InvalidOperationException("The feature action bar retained stale rows after widening.");
                }
                bar.Actions[0].Command.Execute(null);
                if (clicked != 1) throw new InvalidOperationException("Wrapping changed native action routing.");
            }
            finally { bar.ClearActions(); window.Close(); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static int AssertFits(FeatureActionBar bar, int count)
        {
            var buttons = Descendants(bar).OfType<Button>().Where(button => button.DataContext is FeatureAction).ToArray();
            if (buttons.Length != count) throw new InvalidOperationException("Wrapped actions were lost from the visual tree.");
            var bounds = new List<Rect>();
            foreach (var button in buttons)
            {
                var rect = new Rect(button.TranslatePoint(new Point(), bar), button.RenderSize);
                if (button.ActualHeight < 29.5 || rect.Left < -.5 || rect.Top < -.5 ||
                    rect.Right > bar.ActualWidth + .5 || rect.Bottom > bar.ActualHeight + .5)
                    throw new InvalidOperationException("A feature button is compressed or clipped by its action bar.");
                var caption = Descendants(button).OfType<AccessText>().Single(text => text.Text == button.Content as string);
                var textBounds = new Rect(caption.TranslatePoint(new Point(), button), caption.RenderSize);
                if (caption.ActualHeight < caption.DesiredSize.Height - .5 || textBounds.Left < -.5 || textBounds.Top < -.5 ||
                    textBounds.Right > button.ActualWidth + .5 || textBounds.Bottom > button.ActualHeight + .5)
                    throw new InvalidOperationException("A feature button clips its caption at the current font size.");
                if (bounds.Any(previous => Rect.Intersect(previous, rect) is { IsEmpty: false, Width: > .5, Height: > .5 }))
                    throw new InvalidOperationException("Wrapped feature buttons overlap.");
                bounds.Add(rect);
            }
            return bounds.Select(rect => Math.Round(rect.Top)).Distinct().Count();
        }
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        #endregion END private helpers
    }
}
