using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>Provides one themed selectable feature-tab presentation for game workspaces.</summary>
    public sealed class GameFeatureNavigation : ListBox
    {
        /// <summary>Creates standard wrapping tabs with common focus, selection and scrolling behavior.</summary>
        public GameFeatureNavigation()
        {
            SetResourceReference(StyleProperty, typeof(ListBox));
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
            ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
            System.Windows.Automation.AutomationProperties.SetName(this, "Game feature navigation");
        }
    }
}
