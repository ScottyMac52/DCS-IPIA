using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Heimdall.App
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

        /// <summary>Opt-in smart navigation shared by game drawings; does not change existing viewport behavior.</summary>
        internal void EnableSmartNavigation()
        {
            _viewport.PreviewMouseWheel += SmartWheel;
            _viewport.PreviewMouseDown += BeginPan;
            _viewport.PreviewMouseMove += MovePan;
            _viewport.PreviewMouseUp += EndPan;
            _viewport.LostMouseCapture += (_, _) => _panning = false;
        }

        private bool _panning;
        private Point _panStart;
        private double _panHorizontal;
        private double _panVertical;

        private void SmartWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                var oldScale = _zoom.Value;
                var next = Math.Clamp(oldScale * Math.Pow(1.12, e.Delta / 120.0), _zoom.Minimum, _zoom.Maximum);
                if (Math.Abs(next - oldScale) < 0.00001) { e.Handled = true; return; }
                // Keep the point beneath the cursor stationary after LayoutTransform updates the scroll extent.
                var cursor = e.GetPosition(_viewport);
                var logicalX = (_viewport.HorizontalOffset + cursor.X) / oldScale;
                var logicalY = (_viewport.VerticalOffset + cursor.Y) / oldScale;
                _zoom.Value = next;
                _viewport.UpdateLayout();
                _viewport.ScrollToHorizontalOffset(logicalX * next - cursor.X);
                _viewport.ScrollToVerticalOffset(logicalY * next - cursor.Y);
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                _viewport.ScrollToHorizontalOffset(_viewport.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
            // Plain wheel retains native WPF vertical scrolling.
        }

        private void BeginPan(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            _panStart = e.GetPosition(_viewport);
            _panHorizontal = _viewport.HorizontalOffset;
            _panVertical = _viewport.VerticalOffset;
            _panning = _viewport.CaptureMouse();
            if (_panning) e.Handled = true;
        }

        private void MovePan(object sender, MouseEventArgs e)
        {
            if (!_panning) return;
            if (e.MiddleButton != MouseButtonState.Pressed) { EndPan(sender, null); return; }
            var at = e.GetPosition(_viewport);
            _viewport.ScrollToHorizontalOffset(_panHorizontal + _panStart.X - at.X);
            _viewport.ScrollToVerticalOffset(_panVertical + _panStart.Y - at.Y);
            e.Handled = true;
        }

        private void EndPan(object sender, MouseButtonEventArgs? e)
        {
            if (!_panning) return;
            _panning = false;
            _viewport.ReleaseMouseCapture();
            if (e is not null) e.Handled = true;
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
