using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Infrastructure;

/// <summary>SQLite editor payload; module configuration remains in the relational tables.</summary>
/// <param name="Pages">Saved page identities.</param>
/// <param name="Images">Saved PNG bytes.</param>
/// <param name="Diagrams">Saved canonical SVGs.</param>
/// <param name="ModuleCommands">Saved aircraft commands.</param>
/// <param name="UiCommands">Saved UI Layer commands.</param>
/// <param name="Assignments">Saved module-owned assignments.</param>
/// <param name="Labels">Saved displayed labels.</param>
internal sealed record EditorWorkspacePayload(
 IReadOnlyList<KneeboardPage> Pages,
 IReadOnlyList<KneeboardImageAsset> Images,
 IReadOnlyList<UiLayerDiagram> Diagrams,
 IReadOnlyList<CommandPaletteEntry> ModuleCommands,
 IReadOnlyList<DcsCommandDefinition> UiCommands,
 IReadOnlyList<DraftAssignment> Assignments,
 IReadOnlyDictionary<string,string> Labels);
