using System.Text.Json;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Retains relational configuration and editor data for verified module rollback.</summary>
    /// <param name="Configuration">Serialized domain configuration reconstructed through validated constructors.</param>
    /// <param name="Editor">Complete editor payload and baseline reference.</param>
    internal sealed record ModuleUpdateHistoryPayload(JsonElement Configuration, EditorWorkspacePayload Editor);
}
