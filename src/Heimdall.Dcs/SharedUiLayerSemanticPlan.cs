using Heimdall.Application;

namespace Heimdall.Dcs
{
    /// <summary>Reviewed semantic module draft and ordered evidence; persistence remains explicit.</summary>
    /// <param name="Workspace">Proposed module preserving overrides and isolated aircraft state.</param>
    /// <param name="Changes">Semantic review rows.</param>
    public sealed record SharedUiLayerSemanticPlan(EditorWorkspaceSnapshot Workspace,
        IReadOnlyList<SharedUiLayerSemanticChange> Changes);
}

