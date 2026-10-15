using System.IO;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Loads bundled, redistributable examples independently of user files and the working directory.</summary>
    public static class TrainingAssets
    {
        private static readonly string[] ScenarioNames = ["scratch.json", "existing.json"];
        #region BEGIN Public
        /// <summary>Reads one known embedded asset; callers do not supply filesystem paths.</summary>
        public static string Read(string name)
        {
            using var stream = typeof(TrainingAssets).Assembly.GetManifestResourceStream("Dcs.Ipia.App.Training.Assets." + name)
                ?? throw new InvalidDataException("Missing bundled training asset: " + name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        /// <summary>Gets both validated workflows using the driver's supported action vocabulary.</summary>
        public static IReadOnlyList<TrainingScenario> Scenarios() => ScenarioNames
            .Select(name => TrainingScenario.Read(Read(name), TrainingActions.Supported)).ToArray();
        #endregion END Public
    }
}
