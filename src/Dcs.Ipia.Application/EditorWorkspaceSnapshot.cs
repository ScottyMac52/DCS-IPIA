using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>All data needed to reopen a module editor from the local database.</summary>
    /// <param name="Configuration">Stored module, profiles, modifiers and bindings.</param>
    /// <param name="Pages">Stored kneeboard page identities.</param>
    /// <param name="Images">Stored kneeboard image bytes.</param>
    /// <param name="Diagrams">Stored draw.io generated SVGs and authored controls.</param>
    /// <param name="ModuleCommands">Stored named aircraft command catalog.</param>
    /// <param name="UiCommands">Stored standard UI Layer command catalog.</param>
    /// <param name="Assignments">Current module and module-local UI Layer assignments.</param>
    /// <param name="Labels">Displayed callout label overrides by assignment slot.</param>
    /// <param name="Sources">Original imported module configuration text for lossless recovery.</param>
    public sealed record EditorWorkspaceSnapshot(ModuleConfiguration Configuration, IReadOnlyList<KneeboardPage> Pages,
                                                 IReadOnlyList<KneeboardImageAsset> Images,
                                                 IReadOnlyList<UiLayerDiagram> Diagrams,
                                                 IReadOnlyList<CommandPaletteEntry> ModuleCommands,
                                                 IReadOnlyList<DcsCommandDefinition> UiCommands,
                                                 IReadOnlyList<DraftAssignment> Assignments,
                                                 IReadOnlyDictionary<string, string> Labels,
                                                 IReadOnlyDictionary<string, string>? Sources = null)
    {
        #region BEGIN Public
        /// <summary>Gets the verified default revision baseline; effective module sources and overrides remain separate.</summary>
        public SharedUiLayerWorkspace? SharedDefault { get; init; }
        #endregion END Public
    }
}
