using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DcsIpia.Presentation
{

    /// <summary>Fits an existing drawing to its viewport while preserving explicit manual zoom.</summary>
    internal sealed class DiagramViewport
    {
        private readonly ScrollViewer _viewport;
        private readonly FrameworkElement _drawing;
        private readonly Slider _zoom;
        private readonly string _key;
        private bool _fit = true;
        private bool _updating;
        private bool _restored;

        internal DiagramViewport(ScrollViewer viewport, FrameworkElement drawing, Slider zoom, string key)
        {
            _viewport = viewport;
            _drawing = drawing;
            _zoom = zoom;
            _key = key;
            viewport.Loaded += (_, _) =>
            {
                if (!_restored)
                {
                    _restored = true;
                    var saved = WorkspaceLayout.GetPersistenceEnabled(viewport)
                        ? WorkspaceLayoutStore.Read(key, 0, 0, zoom.Maximum) : 0;
                    _fit = saved == 0;
                    if (!_fit) zoom.Value = Math.Clamp(saved, zoom.Minimum, zoom.Maximum);
                }
                QueueFit();
            };
            viewport.SizeChanged += (_, _) => QueueFit();
            drawing.SizeChanged += (_, _) => QueueFit();
            zoom.ValueChanged += (_, _) =>
            {
                if (_updating) return;
                _fit = false;
                ApplyScale();
                Save();
            };
        }

        internal void Fit()
        {
            _fit = true;
            QueueFit();
            Save();
        }

        private void Save()
        {
            if (_restored && WorkspaceLayout.GetPersistenceEnabled(_viewport))
                WorkspaceLayoutStore.Write(_key, _fit ? 0 : _zoom.Value);
        }

        private void QueueFit()
        {
            if (!_viewport.IsLoaded) return;
            _viewport.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateFit));
        }

        private void UpdateFit()
        {
            if (!_fit || !double.IsFinite(_drawing.Width) || !double.IsFinite(_drawing.Height) ||
                _drawing.Width <= 0 || _drawing.Height <= 0 || _viewport.ActualWidth <= 24 || _viewport.ActualHeight <= 24) return;
            var scale = Math.Min((_viewport.ActualWidth - 24) / _drawing.Width,
                                 (_viewport.ActualHeight - 24) / _drawing.Height);
            _updating = true;
            try { _zoom.Value = Math.Clamp(scale, _zoom.Minimum, _zoom.Maximum); }
            finally { _updating = false; }
            ApplyScale();
            _viewport.ScrollToHorizontalOffset(0);
            _viewport.ScrollToVerticalOffset(0);
        }

        private void ApplyScale() => _drawing.LayoutTransform = new ScaleTransform(_zoom.Value, _zoom.Value);
    }
}
