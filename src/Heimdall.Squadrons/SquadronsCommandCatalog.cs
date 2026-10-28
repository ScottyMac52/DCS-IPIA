using System.Text;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Builds an independent catalog from captured concepts without assuming a fixed number of binding slots.</summary>
    internal sealed class SquadronsCommandCatalog(string context, SquadronsNativeDocument document) : ICommandCatalog
    {
        #region BEGIN Public
        /// <summary>Gets the owning adapter.</summary>
        public string SimId => "star-wars-squadrons";
        /// <summary>Gets independent commands, retaining unknown concepts under Other.</summary>
        public IReadOnlyList<SimCommand> Commands
        {
            get
            {
                var captured = document.Bindings.GroupBy(x => x.Concept, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => (IReadOnlyCollection<SquadronsBindingRow>)x.ToArray(), StringComparer.Ordinal);
                return SquadronsKnownConcepts.All.Concat(captured.Keys).Distinct(StringComparer.Ordinal)
                    .Select(concept => new SimCommand
                    {
                        Id = context + "/" + concept, Context = context,
                        DisplayName = Display(concept), Category = Category(concept),
                        Type = captured.TryGetValue(concept, out var rows) ? InputType(rows) : CommandType.Other,
                        Metadata = new() { ["squadrons.concept"] = concept }
                    }).ToArray();
            }
        }
        /// <summary>Finds an exact scoped command.</summary>
        public SimCommand? Find(string id) => Commands.FirstOrDefault(x => x.Id == id);
        #endregion END Public
        #region BEGIN private helpers
        private static string Category(string concept) => concept.Split('.')[0] switch
        {
            "IncomDefaultInputConcepts" => "Menus & General",
            "IncomSoldierInputConcepts" => "Hangar & Briefing Room",
            "IncomStarshipInputConcepts" => "Flight", _ => "Other"
        };
        private static CommandType InputType(IEnumerable<SquadronsBindingRow> rows)
        {
            var types = rows.Select(x => x.Input?.Type).Where(x => x.HasValue).Distinct().ToArray();
            return types.Length == 1 ? types[0]!.Value : CommandType.Other;
        }
        private static string Display(string concept)
        {
            var name = concept[(concept.LastIndexOf('.') + 1)..];
            if (name.StartsWith("Concept", StringComparison.Ordinal)) name = name[7..];
            var text = new StringBuilder();
            for (var index = 0; index < name.Length; index++)
            {
                if (index > 0 && char.IsUpper(name[index]) && char.IsLower(name[index - 1])) text.Append(' ');
                text.Append(name[index]);
            }
            return text.ToString();
        }
        #endregion END private helpers
    }
}
