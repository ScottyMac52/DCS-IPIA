using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Dcs
{
    /// <summary>Returns independent commands pinned to a DCS profile, never a global cross-context name match.</summary>
    internal sealed class DcsSimCommandCatalog(IEnumerable<SimCommand> commands) : CommandCatalogBase
    {
        private readonly SimCommand[] _commands = commands.DistinctBy(x => x.Id).ToArray();
        #region BEGIN Public
        /// <summary>Gets the DCS backend identifier.</summary>
        public override string SimId => "dcs";
        /// <summary>Gets independent catalog command copies.</summary>
        public override IReadOnlyList<SimCommand> Commands => JsonSerializer.Deserialize<SimCommand[]>(JsonSerializer.Serialize(_commands))!;
        #endregion END Public
    }
}
