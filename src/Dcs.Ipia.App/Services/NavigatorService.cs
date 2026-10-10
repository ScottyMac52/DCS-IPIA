using System;

namespace DcsIpia.Presentation.Services
{
    public class NavigatorService : INavigatorService
    {
        private object? _current;

        public object? Current => _current;

        public event EventHandler? CurrentChanged;

        public void NavigateTo(object? target)
        {
            if (ReferenceEquals(_current, target))
                return;

            _current = target;
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
