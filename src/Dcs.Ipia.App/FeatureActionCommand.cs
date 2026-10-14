using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DcsIpia.Presentation
{
    /// <summary>Wraps an existing view-owned button and routed handler as an ICommand.</summary>
    internal sealed class FeatureActionCommand : ICommand, IDisposable
    {
        private readonly Button _source;
        private bool _disposed;

        #region BEGIN Public
        /// <summary>Observes source button availability.</summary>
        public FeatureActionCommand(Button source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.IsEnabledChanged += EnabledChanged;
        }
        /// <summary>Raises when the view changes availability of an action.</summary>
        public event EventHandler? CanExecuteChanged;
        /// <summary>Preserves the effective enabled state of the view-owned button.</summary>
        public bool CanExecute(object? parameter) => !_disposed && _source.IsEnabled;
        /// <summary>Routes clicks to the original feature handler exactly once.</summary>
        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
                _source.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, _source));
        }
        /// <summary>Detaches the previous view on navigation or shell disposal.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.IsEnabledChanged -= EnabledChanged;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion END Public

        #region BEGIN private helpers
        private void EnabledChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        #endregion END private helpers
    }
}
