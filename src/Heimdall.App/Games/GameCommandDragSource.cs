using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Domain.MultiSim;

namespace Heimdall.App.Games
{
    /// <summary>Provides threshold-based command dragging without simulator-specific mouse handling.</summary>
    internal sealed class GameCommandDragSource : IDisposable
    {
        private readonly ListBox _list;
        private readonly string _format;
        private readonly Func<bool> _enabled;
        private readonly Action<SimCommand>? _starting;
        private readonly Action<DragDropEffects>? _finished;
        private Point? _start;
        private SimCommand? _pressed;
        internal GameCommandDragSource(ListBox list, string format, Func<bool> enabled, Action<SimCommand>? starting = null, Action<DragDropEffects>? finished = null)
        {
            _list = list; _format = format; _enabled = enabled; _starting = starting; _finished = finished;
            list.PreviewMouseLeftButtonDown += Pressed; list.PreviewMouseMove += Moved;
        }
        #region BEGIN Public
        /// <summary>Detaches native interaction callbacks when the owning workspace closes.</summary>
        public void Dispose() { _list.PreviewMouseLeftButtonDown -= Pressed; _list.PreviewMouseMove -= Moved; }
        #endregion END Public
        #region BEGIN private helpers
        private void Pressed(object sender, MouseButtonEventArgs e)
        {
            var item = e.OriginalSource is DependencyObject origin ? ItemsControl.ContainerFromElement(_list, origin) as ListBoxItem : null;
            _pressed = item?.Content as SimCommand;
            _start = _pressed is null ? null : e.GetPosition(_list);
            if (_pressed is not null) _list.SelectedItem = _pressed;
        }
        private void Moved(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { _start = null; _pressed = null; return; }
            if (_start is not { } start || !_enabled() || _pressed is not { } command) return;
            var at = e.GetPosition(_list);
            if (Math.Abs(at.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(at.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _start = null; _pressed = null; _starting?.Invoke(command);
            var effect = DragDrop.DoDragDrop(_list, new DataObject(_format, command.Id), DragDropEffects.Copy);
            _finished?.Invoke(effect);
        }
        #endregion END private helpers
    }
}
