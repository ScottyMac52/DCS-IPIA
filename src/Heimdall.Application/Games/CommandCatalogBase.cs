using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>Shares exact command lookup while leaving native command projection and ownership to each adapter.</summary>
    public abstract class CommandCatalogBase : ICommandCatalog
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public abstract string SimId { get; }
        /// <inheritdoc/>
        public abstract IReadOnlyList<SimCommand> Commands { get; }
        /// <inheritdoc/>
        public SimCommand? Find(string id) => Commands.SingleOrDefault(command => string.Equals(command.Id, id, StringComparison.Ordinal));
        #endregion END Public
    }
}
