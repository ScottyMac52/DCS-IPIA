using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Registers a lazy feature and its optional availability policy with the shared window.</summary>
    public sealed record WorkspaceFeature(string Key, Func<UserControl> Create, Func<string?>? UnavailableReason = null);
}
