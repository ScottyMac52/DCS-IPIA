using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Application;

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
        private bool _panEnabled;
        private bool _panning;
        private Point _panStart;
        private double _panHorizontal;
        private double _panVertical;

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
            viewport.PreviewMouseWheel += OnPreviewMouseWheel;
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

        /// <summary>Enables middle-button panning independently of the canonical wheel policy.</summary>
        internal void EnablePanning()
        {
            if (_panEnabled) return;
            _panEnabled = true;
            _viewport.PreviewMouseDown += BeginPan;
            _viewport.PreviewMouseMove += MovePan;
            _viewport.PreviewMouseUp += EndPan;
            _viewport.LostMouseCapture += (_, _) => _panning = false;
            _viewport.Unloaded += (_, _) => ReleasePan();
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
            if (e.MiddleButton != MouseButtonState.Pressed) { ReleasePan(); return; }
            var at = e.GetPosition(_viewport);
            _viewport.ScrollToHorizontalOffset(_panHorizontal + _panStart.X - at.X);
            _viewport.ScrollToVerticalOffset(_panVertical + _panStart.Y - at.Y);
            e.Handled = true;
        }

        private void EndPan(object sender, MouseButtonEventArgs e)
        {
            if (!_panning || e.ChangedButton != MouseButton.Middle) return;
            ReleasePan(); e.Handled = true;
        }

        private void ReleasePan()
        {
            _panning = false;
            if (_viewport.IsMouseCaptured) _viewport.ReleaseMouseCapture();
        }

        /// <summary>All diagram surfaces share identical wheel gestures. Handling this
        /// at the viewport prevents a nested drawing control from scrolling a parent.</summary>
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0) return;
            var mode = DiagramWheelNavigation.Mode(
                (Keyboard.Modifiers & ModifierKeys.Control) != 0,
                (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            if (mode == DiagramWheelMode.Zoom)
            {
                var before = _zoom.Value;
                var after = DiagramWheelNavigation.Zoom(before, e.Delta, _zoom.Minimum, _zoom.Maximum);
                if (Math.Abs(after - before) > 0.0000001)
                {
                    var point = e.GetPosition(_viewport);
                    var horizontal = _viewport.HorizontalOffset;
                    var vertical = _viewport.VerticalOffset;
                    _zoom.Value = after;
                    // LayoutTransform changes extent on layout. Restore the same
                    // image pixel under the mouse after the new extent is measured.
                    _viewport.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        var ratio = after / before;
                        _viewport.ScrollToHorizontalOffset(
                            DiagramWheelNavigation.AnchoredOffset(horizontal, point.X, ratio));
                        _viewport.ScrollToVerticalOffset(
                            DiagramWheelNavigation.AnchoredOffset(vertical, point.Y, ratio));
                    }));
                }
            }
            else if (mode == DiagramWheelMode.Horizontal)
                _viewport.ScrollToHorizontalOffset(_viewport.HorizontalOffset -
                    DiagramWheelNavigation.ScrollDelta(e.Delta));
            else
                _viewport.ScrollToVerticalOffset(_viewport.VerticalOffset -
                    DiagramWheelNavigation.ScrollDelta(e.Delta));
            e.Handled = true;
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
