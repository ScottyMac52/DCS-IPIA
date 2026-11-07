
namespace Heimdall.Application.Games
{
    /// <summary>Typed adapters report native session facts without introducing another busy owner.</summary>
    public sealed record NativeWorkspaceFacts(string Key, bool Open, bool ReadOnly, string ReadOnlyReason, bool Dirty, bool New, string SavedName, WorkspaceCapabilities Capabilities);
}
