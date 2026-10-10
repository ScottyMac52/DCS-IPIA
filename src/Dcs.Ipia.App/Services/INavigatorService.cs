using System;

namespace DcsIpia.Presentation.Services
{
    public interface INavigatorService
    {
        /// <summary>
        /// The currently active view model or view object.
        /// </summary>
        object? Current { get; }

        /// <summary>
        /// Navigate to the given view object (usually a ViewModel or View).
        /// </summary>
        /// <param name="target">The target view object.</param>
        void NavigateTo(object? target);

        /// <summary>
        /// Event raised when the current view changes.
        /// </summary>
        event EventHandler? CurrentChanged;
    }
}
