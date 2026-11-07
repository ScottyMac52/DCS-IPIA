
namespace Heimdall.Application.Games
{
    /// <summary>Immutable native selection addresses; selection never edits or persists data.</summary>
    public sealed record WorkspaceSelection(string? ControllerId, string? CommandId, string? SlotId, string? AlternativeId, string? InputId, string NativeScope);
}
