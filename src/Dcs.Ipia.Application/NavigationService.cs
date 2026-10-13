using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Application
{
    /// <summary>
    /// Implements a navigation service that allows navigating between different features of the application.
    /// </summary>
    public sealed class NavigationService : INavigationService
    {
        private readonly HashSet<string> _knownKeys;
        private string? _current;

        public NavigationService(IEnumerable<string> knownFeatureKeys)
        {
            ArgumentNullException.ThrowIfNull(knownFeatureKeys);
            _knownKeys = new HashSet<string>(knownFeatureKeys, StringComparer.OrdinalIgnoreCase);
        }

        public string? CurrentFeatureKey => _current;

        public event EventHandler? CurrentFeatureChanged;

        public bool CanNavigateTo(string featureKey) =>
            !string.IsNullOrWhiteSpace(featureKey) && _knownKeys.Contains(featureKey);

        public void NavigateTo(string featureKey)
        {
            if (!CanNavigateTo(featureKey))
                throw new ArgumentException($"Unknown feature key: {featureKey}", nameof(featureKey));

            if (string.Equals(_current, featureKey, StringComparison.OrdinalIgnoreCase))
                return; // no-op

            _current = featureKey;
            CurrentFeatureChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
