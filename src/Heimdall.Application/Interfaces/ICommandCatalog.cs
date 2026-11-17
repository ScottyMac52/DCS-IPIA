using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Catalog of bindable commands for a specific sim.</summary>
    public interface ICommandCatalog
    {
        #region BEGIN Public

        /// <summary>Gets the sim identifier this catalog belongs to.</summary>
        string SimId { get; }

        /// <summary>Gets all commands in the catalog.</summary>
        IReadOnlyList<SimCommand> Commands { get; }

        /// <summary>Finds a command by its exact, ordinal, case-sensitive internal identifier without trimming or alias matching.</summary>
        /// <param name="id">The complete command identifier, including its native scope.</param>
        /// <returns>The unique matching command, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">More than one command has the requested identifier.</exception>
        SimCommand? Find(string id);

        #endregion END Public
    }
}
