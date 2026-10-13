using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DcsIpia.Presentation
{
    /// <summary>Arranges workspace actions and locates their buttons by caption.</summary>
    public class ActionBar : WrapPanel
    {
        #region BEGIN Public
        public ActionBar() => Orientation = Orientation.Horizontal;

        /// <summary>Finds a button by its displayed caption within a visual tree.</summary>
        public static Button? FindButton(DependencyObject root, string content)
        {
            if (root is null) return null;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Button button && button.Content as string == content)
                    return button;
                var found = FindButton(child, content);
                if (found is not null) return found;
            }
            return null;
        }
        #endregion END Public
    }
}
