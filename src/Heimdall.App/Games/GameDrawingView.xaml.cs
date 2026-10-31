using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Heimdall.App.Games
{
    /// <summary>Owns the common zoom, direct canvas viewport, input fallback and drawing hints.</summary>
    public partial class GameDrawingView : UserControl
    {
        private Expander? _disclosure;
        private bool _boundsQueued;
        /// <summary>Creates the shared presentation without native assignment or acquisition logic.</summary>
        public GameDrawingView()
        {
            InitializeComponent();
            Loaded += AttachBounds;
            Unloaded += DetachBounds;
            SizeChanged += BoundsChanged;
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void AttachBounds(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, this)) return;
            for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is Expander disclosure) { _disclosure = disclosure; break; }
            if (_disclosure is not null)
            {
                _disclosure.SizeChanged += BoundsChanged;
                _disclosure.Expanded += DisclosureChanged;
                _disclosure.Collapsed += DisclosureChanged;
            }
            QueueBounds();
        }
        private void DetachBounds(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, this)) return;
            if (_disclosure is not null)
            {
                _disclosure.SizeChanged -= BoundsChanged;
                _disclosure.Expanded -= DisclosureChanged;
                _disclosure.Collapsed -= DisclosureChanged;
                _disclosure = null;
            }
            Viewport.MaxHeight = double.PositiveInfinity;
        }
        private void BoundsChanged(object sender, SizeChangedEventArgs e) => QueueBounds();
        private void DisclosureChanged(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, _disclosure)) return;
            if (_disclosure?.IsExpanded == false) Viewport.MaxHeight = double.PositiveInfinity;
            else QueueBounds();
        }
        // Expanders can measure their content with unbounded height. Limit the scroll
        // host to the arranged drawing pane so excess image height becomes scroll extent.
        private void QueueBounds()
        {
            if (_boundsQueued || !IsLoaded) return;
            _boundsQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _boundsQueued = false;
                if (!IsLoaded || _disclosure?.IsExpanded == false) return;
                FrameworkElement boundary = _disclosure is null ? this : _disclosure;
                if (boundary.ActualHeight <= 0) return;
                var top = Viewport.TranslatePoint(new Point(0, 0), boundary).Y;
                var maximum = Math.Max(24, boundary.ActualHeight - top - 4);
                if (double.IsFinite(maximum) && Math.Abs(Viewport.MaxHeight - maximum) > .5)
                    Viewport.MaxHeight = maximum;
            }));
        }
        #endregion END private helpers
    }
}
