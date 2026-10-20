using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>Owns a native catalog; each query returns independent mutable command objects.</summary>
    internal sealed class EliteCommandCatalog(string context, EliteNativeDocument native) : ICommandCatalog
    {
        #region BEGIN Public
        /// <summary>Gets the catalog backend.</summary>
        public string SimId => "elite-dangerous";
        /// <summary>Gets all native commands, including commands with no assignments.</summary>
        public IReadOnlyList<SimCommand> Commands => native.Commands.Select(x => new SimCommand
        {
            Id = EliteProfileProjection.CommandId(context, x.Name.LocalName), DisplayName = EliteCommandNames.Display(x.Name.LocalName),
            Category = EliteCommandSections.ForCommand(x.Name.LocalName), Context = context,
            Type = x.Element("Binding") is null ? CommandType.Button : CommandType.Axis,
            Metadata = x.Element("ToggleOn") is { } toggle
                ? new() { ["ed.toggleOn"] = EliteNativeDocument.Attribute(toggle, "Value") } : new()
        }).ToArray();
        /// <summary>Finds an exact scoped command.</summary>
        public SimCommand? Find(string id) => Commands.FirstOrDefault(x => x.Id == id);
        #endregion END Public
    }
}
