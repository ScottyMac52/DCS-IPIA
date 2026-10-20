using Heimdall.Application.Interfaces;

namespace Heimdall.Application
{
    /// <summary>
    /// Implements a navigation service that allows navigating between different features of the application.
    /// </summary>
    public sealed class NavigationService : INavigationService
    {
        private readonly HashSet<string> _knownKeys;
        private string? _current;

        #region BEGIN Public
        public NavigationService(IEnumerable<string> knownFeatureKeys)
        {
            ArgumentNullException.ThrowIfNull(knownFeatureKeys);
            _knownKeys = new HashSet<string>(knownFeatureKeys, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the selected feature key, or null before the first navigation.</summary>
        public string? CurrentFeatureKey => _current;

        /// <summary>Occurs when navigation selects a different feature.</summary>
        public event EventHandler? CurrentFeatureChanged;

        /// <summary>Reports whether the supplied key identifies a known feature.</summary>
        public bool CanNavigateTo(string featureKey) =>
            !string.IsNullOrWhiteSpace(featureKey) && _knownKeys.Contains(featureKey);

        /// <summary>Selects a known feature and notifies subscribers when the selection changes.</summary>
        public void NavigateTo(string featureKey)
        {
            if (!CanNavigateTo(featureKey))
                throw new ArgumentException($"Unknown feature key: {featureKey}", nameof(featureKey));

            if (string.Equals(_current, featureKey, StringComparison.OrdinalIgnoreCase))
                return; // no-op

            _current = featureKey;
            CurrentFeatureChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion END Public
    }
}
