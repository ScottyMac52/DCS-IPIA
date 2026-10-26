using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Heimdall.App.Games
{
    /// <summary>Routes same-workspace drag and keyboard moves through a game-specific move operation.</summary>
    internal sealed class GameControllerReordering : IDisposable
    {
        private readonly ListBox _list;
        private readonly Action<string, string, bool> _move;
        private readonly Func<string, bool> _canMove;
        private readonly string _format = "heimdall.elite.controller." + Guid.NewGuid().ToString("N");
        private Point _origin;
        private string? _source;
        internal GameControllerReordering(ListBox list, Action<string, string, bool> move, Func<string, bool>? canMove = null)
        {
            _list = list; _move = move; _canMove = canMove ?? (_ => true);
            list.PreviewMouseLeftButtonDown += PointerDown;
            list.PreviewMouseMove += PointerMove;
            list.DragOver += DragOver; list.Drop += Drop; list.PreviewKeyDown += KeyDown;
        }
        #region BEGIN Public
        /// <summary>Detaches handlers when the owning workspace closes.</summary>
        public void Dispose()
        {
            _list.PreviewMouseLeftButtonDown -= PointerDown;
            _list.PreviewMouseMove -= PointerMove;
            _list.DragOver -= DragOver; _list.Drop -= Drop; _list.PreviewKeyDown -= KeyDown;
        }
        /// <summary>Moves the selected controller by one place using the same boundary as drag and drop.</summary>
        internal void MoveSelected(int direction)
        {
            var index = _list.SelectedIndex; var target = index + direction;
            if (index < 0 || target < 0 || target >= _list.Items.Count || Math.Abs(direction) != 1) return;
            var sourceId = ((GameDeviceChoice)_list.Items[index]).Id;
            var targetId = ((GameDeviceChoice)_list.Items[target]).Id;
            if (_canMove(sourceId) && _canMove(targetId)) _move(sourceId, targetId, direction > 0);
        }
        /// <summary>Creates a payload scoped to this workspace, excluding command and foreign-window drags.</summary>
        internal DataObject Payload(string id) => new(_format, id);
        /// <summary>Accepts only current references from this workspace, leaving self and foreign drops unchanged.</summary>
        internal bool DropAt(IDataObject data, string target, bool after)
        {
            if (!data.GetDataPresent(_format) || data.GetData(_format) is not string source || source == target
                || !_canMove(source) || !_canMove(target)) return false;
            var ids = _list.Items.Cast<GameDeviceChoice>().Select(x => x.Id).ToArray();
            if (!ids.Contains(source, StringComparer.Ordinal) || !ids.Contains(target, StringComparer.Ordinal)) return false;
            _move(source, target, after); return true;
        }
        #endregion END Public
        #region BEGIN private helpers
        private ListBoxItem? Container(object original) => original is DependencyObject element
            ? ItemsControl.ContainerFromElement(_list, element) as ListBoxItem : null;
        private void PointerDown(object sender, MouseButtonEventArgs e)
        {
            _origin = e.GetPosition(_list);
            _source = Container(e.OriginalSource)?.DataContext is GameDeviceChoice choice && _canMove(choice.Id) ? choice.Id : null;
        }
        private void PointerMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { _source = null; return; }
            var point = e.GetPosition(_list);
            if (_source is null || (Math.Abs(point.X - _origin.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(point.Y - _origin.Y) < SystemParameters.MinimumVerticalDragDistance)) return;
            var source = _source; _source = null;
            DragDrop.DoDragDrop(_list, Payload(source), DragDropEffects.Move);
        }
        private void DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(_format) && Container(e.OriginalSource)?.DataContext is GameDeviceChoice choice && _canMove(choice.Id)
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }
        private void Drop(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (Container(e.OriginalSource) is { DataContext: GameDeviceChoice target } container
                && DropAt(e.Data, target.Id, e.GetPosition(container).Y > container.ActualHeight / 2))
                e.Effects = DragDropEffects.Move;
            e.Handled = true; _source = null;
        }
        private void KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || e.Key is not (Key.Up or Key.Down)) return;
            MoveSelected(e.Key == Key.Up ? -1 : 1); e.Handled = true;
        }
        #endregion END private helpers
    }
}
