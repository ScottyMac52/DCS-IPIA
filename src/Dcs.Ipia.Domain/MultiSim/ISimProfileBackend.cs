namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// Adapter that translates between the intermediate profile model and a specific sim's native format.
/// </summary>
public interface ISimProfileBackend
{
    /// <summary>Gets the sim identifier (e.g. "dcs", "msfs2020", "elitedangerous").</summary>
    string SimId { get; }

    /// <summary>Gets the command catalog for this sim.</summary>
    ICommandCatalog Catalog { get; }

    /// <summary>Loads a profile from the sim's native location or file.</summary>
    ProfileDocument Load(string path);

    /// <summary>Saves a profile to the sim's native format.</summary>
    void Save(ProfileDocument profile, string destination);

    /// <summary>Resolves a physical device into the identity expected by this sim.</summary>
    PhysicalDeviceIdentity ResolveDevice(PhysicalDevice device);

    /// <summary>Gets known default profile search paths for this sim.</summary>
    IEnumerable<string> GetDefaultProfilePaths();
}
