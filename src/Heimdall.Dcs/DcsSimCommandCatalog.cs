using System.Text.Json;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Dcs
{
    /// <summary>Returns independent commands pinned to a DCS profile, never a global cross-context name match.</summary>
    internal sealed class DcsSimCommandCatalog(IEnumerable<SimCommand> commands) : ICommandCatalog
    {
        private readonly SimCommand[] _commands = commands.DistinctBy(x => x.Id).ToArray();
        #region BEGIN Public
        /// <summary>Gets the DCS backend identifier.</summary>
        public string SimId => "dcs";
        /// <summary>Gets independent catalog command copies.</summary>
        public IReadOnlyList<SimCommand> Commands => JsonSerializer.Deserialize<SimCommand[]>(JsonSerializer.Serialize(_commands))!;
        /// <summary>Finds one exact scoped command.</summary>
        /// <param name="id">Backend/context/section/native command identity.</param>
        /// <returns>An independent command, or null.</returns>
        public SimCommand? Find(string id) => Commands.SingleOrDefault(x => x.Id == id);
        #endregion END Public
    }
}
