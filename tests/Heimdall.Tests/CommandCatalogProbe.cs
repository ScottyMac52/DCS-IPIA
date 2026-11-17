using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Tests
{
    /// <summary>Supplies controlled command identities to exercise the shared lookup contract.</summary>
    internal sealed class CommandCatalogProbe(IReadOnlyList<SimCommand> commands) : CommandCatalogBase
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public override string SimId => "probe";
        /// <inheritdoc/>
        public override IReadOnlyList<SimCommand> Commands => commands;
        #endregion END Public
    }
}
