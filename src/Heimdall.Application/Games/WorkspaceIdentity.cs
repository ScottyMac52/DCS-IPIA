
namespace Heimdall.Application.Games
{
    /// <summary>Native stable identity plus a context-owned replacement epoch.</summary>
    public readonly record struct WorkspaceIdentity(string Key, long Generation);
}
