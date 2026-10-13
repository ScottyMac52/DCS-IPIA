using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DcsIpia.Presentation
{
    public class ActionBar : WrapPanel
    {
        public ActionBar() => Orientation = Orientation.Horizontal;

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
    }
}