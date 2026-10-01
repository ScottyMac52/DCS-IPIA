using System.Text.Json;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Maintains explicit hardware choices for independently authored shared profiles.</summary>
    public static class SharedUiLayerHardwareMapping
    {
        private const string Source = "assets/shared/ui-layer/ipia-device-hardware.json";
        #region BEGIN Public
        /// <summary>Stages one profile's hardware choice through the normal default revision workflow.</summary>
        /// <param name="defaults">Current default draft.</param>
        /// <param name="path">Existing joystick source path.</param>
        /// <param name="hardware">Selected pinned hardware.</param>
        /// <returns>Draft with the mapping preserved as shared source evidence.</returns>
        public static SharedUiLayerWorkspace Set(SharedUiLayerWorkspace defaults, string path, WorkspaceHardware hardware)
        {
            if (!SharedUiLayerBindingEditor.Profiles(defaults).Contains(path, StringComparer.Ordinal) ||
                !path.Contains("/joystick/", StringComparison.Ordinal))
                throw new ArgumentException("Select an existing joystick profile.", nameof(path));
            var mappings = Read(defaults);
            mappings[path] = hardware.DeviceId;
            var sources = new Dictionary<string, string>(defaults.Sources, StringComparer.Ordinal) {
                [Source] = JsonSerializer.Serialize(new SortedDictionary<string, string>(mappings, StringComparer.Ordinal)) + "\n"
            };
            return defaults with { Sources = sources, ContentSha256 = null };
        }
        /// <summary>Resolves explicit mappings or unique native filename/physical matches; ambiguity requires a choice.</summary>
        /// <param name="defaults">Saved default revision.</param>
        /// <param name="hardware">Pinned catalog choices.</param>
        /// <returns>Exact graphics for every joystick profile.</returns>
        public static IReadOnlyDictionary<string, UiLayerDiagram> Resolve(SharedUiLayerWorkspace defaults, IEnumerable<WorkspaceHardware> hardware)
        {
            var choices = hardware.ToArray();
            var mappings = Read(defaults);
            var result = new SortedDictionary<string, UiLayerDiagram>(StringComparer.Ordinal);
            foreach (var path in SharedUiLayerBindingEditor.Profiles(defaults).Where(x => x.Contains("/joystick/", StringComparison.Ordinal)))
            {
                var file = Path.GetFileName(path);
                var guid = DcsPhysicalIdentity.FromFileName(file);
                var explicitIds = mappings.TryGetValue(path, out var explicitId) ? new[] { explicitId } : mappings.Where(x => guid is not null &&
                    DcsPhysicalIdentity.FromFileName(Path.GetFileName(x.Key)) == guid).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
                if (explicitIds.Length > 1) throw new InvalidDataException("Conflicting hardware choices for " + file);
                var candidates = explicitIds.Length == 1 ? choices.Where(x => x.DeviceId == explicitIds[0]).ToArray() :
                    choices.Where(x => x.DefaultFileName.Equals(file, StringComparison.OrdinalIgnoreCase) ||
                        guid is not null && DcsPhysicalIdentity.FromFileName(x.DefaultFileName) == guid).ToArray();
                if (candidates.Length == 0 && explicitIds.Length == 0)
                    candidates = choices.Where(x => Name(x.DefaultFileName).Equals(Name(file), StringComparison.OrdinalIgnoreCase)).ToArray();
                var diagrams = candidates.DistinctBy(x => (x.Diagram.Path, JsonSerializer.Serialize(x.Diagram.Controls))).ToArray();
                if (diagrams.Length != 1)
                    throw new InvalidDataException("Choose output hardware for " + file + " in the Devices tab.");
                result.Add(path, diagrams[0].Diagram with { Device = file[..^9], Instance = null, Callouts = [] });
            }
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, string> Read(SharedUiLayerWorkspace defaults) => defaults.Sources.TryGetValue(Source, out var source)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(source) ?? throw new InvalidDataException("Invalid output hardware mappings.") : [];
        private static string Name(string file) => DcsPhysicalIdentity.FromFileName(file) is null ? file[..^9] : DcsPhysicalIdentity.NameFromFileName(file);
        #endregion END private helpers
    }
}
