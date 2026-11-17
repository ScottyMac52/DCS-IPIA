using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Builds a metadata-driven catalog from captured command blocks, retaining cleared and unknown commands.</summary>
    internal sealed class WarThunderCommandCatalog(string context, WarThunderNativeProfile current, WarThunderNativeProfile original) : CommandCatalogBase
    {
        #region BEGIN Public
        /// <summary>Gets the owning backend.</summary>
        public override string SimId => "war-thunder";
        /// <summary>Gets captured commands, including explicit unbound blocks and retained original command identities.</summary>
        public override IReadOnlyList<SimCommand> Commands => original.Bindings.Concat(current.Bindings)
            .GroupBy(binding => binding.Section + "/" + binding.Command, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new SimCommand
            {
                Id = context + "/" + group.Key, Context = context,
                DisplayName = (group.First().Command.StartsWith("ID_", StringComparison.Ordinal) ? group.First().Command[3..] : group.First().Command).Replace('_', ' '),
                Category = group.First().Section == "axes" ? "Native axes" : "Native hotkeys",
                Type = group.First().Section == "axes" ? CommandType.Axis : CommandType.Button,
                Metadata = new()
                {
                    ["war-thunder.section"] = group.First().Section,
                    ["war-thunder.command"] = group.First().Command,
                    ["war-thunder.catalog-coverage"] = "captured",
                    ["war-thunder.parameter-types"] = JsonSerializer.Serialize(group.SelectMany(binding => binding.Fields)
                        .GroupBy(scalar => WarThunderNativeProfile.Name(scalar.Path), StringComparer.Ordinal)
                        .ToDictionary(fields => fields.Key, fields => fields.Select(scalar => scalar.Type.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal))
                }
            }).ToArray();
        #endregion END Public
    }
}
