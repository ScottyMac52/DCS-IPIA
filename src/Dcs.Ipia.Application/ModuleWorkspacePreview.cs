using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Pairs a validated Huey import with its existing committed kneeboard images and shared hardware graphics.</summary>
    /// <param name="Import">The read-only module source import.</param>
    /// <param name="Images">Committed PNGs in kneeboard page order.</param>
    /// <param name="Diagrams">Read-only UI Layer graphics with explicit module assignments.</param>
    public sealed record ModuleWorkspacePreview(ConsumerImportResult Import, IReadOnlyList<KneeboardImageAsset> Images,
                                                IReadOnlyList<UiLayerDiagram> Diagrams)
    {
        #region BEGIN Public
        /// <summary>Gets the exact committed module revision read by the repository loader.</summary>
        public string ModuleCommit { get; init; } = string.Empty;
        /// <summary>Gets staged, unstaged, and new module input paths excluded from the committed import.</summary>
        public IReadOnlyList<string> UncommittedSourcePaths { get; init; } = [];
        /// <summary>Gets graphics problems that did not prevent the module and kneeboard preview.</summary>
        public IReadOnlyList<ImportDiagnostic> GraphicDiagnostics { get; init; } = [];
        /// <summary>Gets the validated DCS-Common UI Layer catalog, even when device graphics are unavailable.</summary>
        public IReadOnlyList<DcsCommandDefinition> UiCommands { get; init; } = [];
        /// <summary>Gets module commands from the scaffold solution linked JSON or DCS HTML source.</summary>
        public IReadOnlyList<CommandPaletteEntry> ModuleCommands { get; init; } = [];
        /// <summary>Describes the linked module command source.</summary>
        public string ModuleCommandSource { get; init; } = "No linked module command source was found.";
        /// <summary>Reports a missing or invalid linked command source without discarding the module preview.</summary>
        public IReadOnlyList<ImportDiagnostic> CommandDiagnostics { get; init; } = [];
        /// <summary>Gets pinned DCS-Common Lua texts needed to rebuild the module's UI Layer without a checkout.</summary>
        public IReadOnlyDictionary<string, string> SharedLuaSources { get; init; } = new Dictionary<string, string>();
        #endregion END Public
    }
}
