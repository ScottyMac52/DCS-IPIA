namespace Dcs.Ipia.Domain.MultiSim
{
    /// <summary>Catalog of bindable commands for a specific sim.</summary>
    public interface ICommandCatalog
    {
        /// <summary>Gets the sim identifier this catalog belongs to.</summary>
        string SimId { get; }

        /// <summary>Gets all commands in the catalog.</summary>
        IReadOnlyList<SimCommand> Commands { get; }

        /// <summary>Finds a command by its internal identifier.</summary>
        /// <param name="id">The command identifier.</param>
        /// <returns>The command, or null if not found.</returns>
        SimCommand? Find(string id);
    }
}
