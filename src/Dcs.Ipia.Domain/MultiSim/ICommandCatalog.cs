namespace Dcs.Ipia.Domain.MultiSim;

/// <summary>
/// Catalog of bindable commands for a specific sim.
/// </summary>
public interface ICommandCatalog
{
    /// <summary>Gets the sim identifier this catalog belongs to (e.g. "dcs", "msfs2020").</summary>
    string SimId { get; }

    /// <summary>Gets all commands in the catalog.</summary>
    IReadOnlyList<SimCommand> Commands { get; }

    /// <summary>Finds a command by its internal identifier.</summary>
    SimCommand? Find(string id);
}
