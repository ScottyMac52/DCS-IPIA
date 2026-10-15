using System.Text.Json;

namespace Dcs.Ipia.Application
{
    /// <summary>Versioned, declarative training content with a fixed action vocabulary.</summary>
    public sealed record TrainingScenario(int SchemaVersion, string Id, string Title, IReadOnlyList<TrainingStep> Steps)
    {
        #region BEGIN Public
        /// <summary>Reads and validates content before any action or file write.</summary>
        public static TrainingScenario Read(string json, IReadOnlySet<string> actions)
        {
            var scenario = JsonSerializer.Deserialize<TrainingScenario>(json)
                ?? throw new ArgumentException("Training scenario is missing.", nameof(json));
            scenario.Validate();
            if (scenario.Steps.Any(x => !actions.Contains(x.Action)))
                throw new ArgumentException("Training scenario contains an unsupported action.", nameof(json));
            return scenario;
        }

        /// <summary>Rejects unsupported versions, missing narration and ambiguous step identities.</summary>
        public void Validate()
        {
            if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title) || Steps is null || Steps.Count == 0 ||
                Steps.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Action) ||
                    string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.Explanation) || string.IsNullOrWhiteSpace(x.Target)) ||
                Steps.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Steps.Count)
                throw new ArgumentException("Invalid training scenario version or step metadata.");
        }
        #endregion END Public
    }
}
