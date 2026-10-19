namespace Dcs.Ipia.Domain.MultiSim
{
    /// <summary>
    /// Adapter that translates between the intermediate profile model
    /// and a specific sim's native format.
    /// </summary>
    public interface ISimProfileBackend
    {
        /// <summary>Gets the sim identifier.</summary>
        string SimId { get; }

        /// <summary>Gets the command catalog for this sim.</summary>
        ICommandCatalog Catalog { get; }

        /// <summary>Loads a profile from the sim's native location.</summary>
        /// <param name="path">The path or location to load from.</param>
        /// <returns>The loaded profile.</returns>
        ProfileDocument Load(string path);

        /// <summary>Saves a profile to the sim's native format.</summary>
        /// <param name="profile">The profile to save.</param>
        /// <param name="destination">The destination path or location.</param>
        void Save(ProfileDocument profile, string destination);

        /// <summary>
        /// Resolves a physical device into the identity expected by this sim.
        /// </summary>
        /// <param name="device">The physical device.</param>
        /// <returns>The resolved identity.</returns>
        PhysicalDeviceIdentity ResolveDevice(Domain.PhysicalDevice device);

        /// <summary>Gets known default profile search paths for this sim.</summary>
        /// <returns>The default profile paths.</returns>
        IEnumerable<string> GetDefaultProfilePaths();
    }
}
