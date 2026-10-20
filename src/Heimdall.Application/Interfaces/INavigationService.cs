namespace Heimdall.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for a navigation service that allows navigating between different features of the application.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>Current feature key (e.g. "Devices", "Modifiers"). Null if none.</summary>
        string? CurrentFeatureKey { get; }

        /// <summary>Raised after a successful NavigateTo.</summary>
        event EventHandler? CurrentFeatureChanged;

        /// <summary>Navigate to a known feature key.</summary>
        void NavigateTo(string featureKey);

        /// <summary>True if the key is registered.</summary>
        bool CanNavigateTo(string featureKey);
    }
}
