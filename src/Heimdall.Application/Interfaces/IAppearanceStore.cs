namespace Heimdall.Application.Interfaces
{
    /// <summary>Persists accepted appearance overrides and managed image content for one profile.</summary>
    public interface IAppearanceStore
    {
        #region BEGIN Public
        /// <summary>Gets the last successfully loaded or committed appearance.</summary>
        AppearanceSettings Current { get; }
        /// <summary>Gets the latest non-fatal load/save diagnostic.</summary>
        string? LastError { get; }
        /// <summary>Reads the current managed image or reports missing/invalid content.</summary>
        byte[]? ReadImage(string? name = null);
        /// <summary>Atomically commits settings with optional normalized PNG content; failure preserves Current.</summary>
        bool TrySave(AppearanceSettings settings, byte[]? image = null, IReadOnlyDictionary<string, byte[]>? images = null);
        #endregion END Public
    }
}
