using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs.SnapshotModels
{
    /// <summary>Versioned, reviewable frozen editor state used for snapshot-only output generation.</summary>
    /// <param name="SchemaVersion">Editor snapshot schema version.</param>
    /// <param name="Configuration">Canonical module configuration snapshot.</param>
    /// <param name="Pages">Kneeboard page identities.</param>
    /// <param name="Images">Pinned source PNG assets, not generated output.</param>
    /// <param name="Diagrams">Pinned canonical hardware SVG and mapped controls.</param>
    /// <param name="ModuleCommands">Aircraft command definitions.</param>
    /// <param name="UiCommands">Shared UI Layer command definitions.</param>
    /// <param name="Assignments">Saved module and module-local UI Layer assignments.</param>
    /// <param name="Labels">Authored label overrides by assignment slot.</param>
    /// <param name="Sources">Original imported source text, retained for parity analysis.</param>
    public sealed record
    EditorSnapshotDocument(int SchemaVersion, JsonElement Configuration, IReadOnlyList<KneeboardPage> Pages,
                           IReadOnlyList<KneeboardImageAsset> Images, IReadOnlyList<UiLayerDiagram> Diagrams,
                           IReadOnlyList<CommandPaletteEntry> ModuleCommands,
                           IReadOnlyList<DcsCommandDefinition> UiCommands, IReadOnlyList<DraftAssignment> Assignments,
                           IReadOnlyDictionary<string, string> Labels, IReadOnlyDictionary<string, string>? Sources)
    {
        #region BEGIN Public
        /// <summary>Gets the verified shared default baseline needed for offline adoption review.</summary>
        public SharedUiLayerWorkspace? SharedDefault { get; init; }
        #endregion END Public
    }
}
