using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Switches composed native feature views and their real action bars for simulator workspaces.</summary>
    public sealed class GameWorkspaceFeatureHost(ContentControl content, FeatureActionBar actions, Action? stopLiveInput = null)
    {
        #region BEGIN Public
        /// <summary>Mounts an optional feature, prepares its controller context, and exposes only its actual actions.
        /// Re-showing the same view refreshes actions without interrupting live input.</summary>
        public void Show(UIElement? feature, Action<UserControl>? prepare = null)
        {
            if (!ReferenceEquals(content.Content, feature))
            {
                stopLiveInput?.Invoke();
                actions.ClearActions();
                content.Content = feature;
            }
            if (feature is UserControl view)
            {
                prepare?.Invoke(view);
                actions.ShowActions(view);
            }
            else actions.ClearActions();
        }
        /// <summary>Releases the current feature/action connections; native views retain their own unload cleanup.</summary>
        public void Clear() => Show(null);
        #endregion END Public
    }
}
