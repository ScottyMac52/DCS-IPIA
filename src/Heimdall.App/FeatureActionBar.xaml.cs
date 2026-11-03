using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Heimdall.App
{
    /// <summary>Displays stable-position buttons owned by the active workspace feature.</summary>
    public partial class FeatureActionBar : UserControl
    {
        public static readonly DependencyProperty MenuGroupProperty = DependencyProperty.RegisterAttached(
            "MenuGroup", typeof(string), typeof(FeatureActionBar), new PropertyMetadata(string.Empty));

        public static string GetMenuGroup(DependencyObject element) => (string)element.GetValue(MenuGroupProperty);
        public static void SetMenuGroup(DependencyObject element, string value) => element.SetValue(MenuGroupProperty, value);

        private readonly List<FeatureActionCommand> _commands = [];

        #region BEGIN Public
        public FeatureActionBar()
        {
            InitializeComponent();
        }

        /// <summary>Optional workspace-specific shared toolbar style; other workspaces retain ActionButton.</summary>
        public static readonly DependencyProperty ButtonStyleProperty = DependencyProperty.Register(nameof(ButtonStyle),
            typeof(Style), typeof(FeatureActionBar), new PropertyMetadata(null));
        /// <summary>Gets or sets the shared style applied to visible action buttons.</summary>
        public Style? ButtonStyle { get => (Style?)GetValue(ButtonStyleProperty); set => SetValue(ButtonStyleProperty, value); }
        /// <summary>Bindable action array, changed with one notification per navigation.</summary>
        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register(nameof(Actions), typeof(FeatureAction[]),
                typeof(FeatureActionBar), new PropertyMetadata(Array.Empty<FeatureAction>()));

        /// <summary>Current feature's actions; an empty array leaves the reserved toolbar visible.</summary>
        public FeatureAction[] Actions
        {
            get => (FeatureAction[])GetValue(ActionsProperty);
            private set => SetValue(ActionsProperty, value);
        }

        public static readonly DependencyProperty ToolbarActionsProperty =
            DependencyProperty.Register(nameof(ToolbarActions), typeof(FeatureAction[]),
                typeof(FeatureActionBar), new PropertyMetadata(Array.Empty<FeatureAction>()));

        public FeatureAction[] ToolbarActions
        {
            get => (FeatureAction[])GetValue(ToolbarActionsProperty);
            private set => SetValue(ToolbarActionsProperty, value);
        }

        /// <summary>Replaces the action set atomically, without collapsing the toolbar in between views.</summary>
        public void ShowActions(DependencyObject view)
        {
            ArgumentNullException.ThrowIfNull(view);
            var buttons = FindSources(view).OrderBy(button => (button.Parent as ActionBar)?.FeatureOrder ?? 0).ToArray();
            var commands = buttons.Select(button => new FeatureActionCommand(button)).ToArray();
            var next = buttons.Select((button, index) => new FeatureAction(
                button.Name.Length > 0 ? button.Name : "action-" + index,
                button.Content?.ToString() ?? "", commands[index], button.ToolTip) { Source = button }).ToArray();

            Actions = next;
            ToolbarActions = next.GroupBy(action => GetMenuGroup(action.Source!) is { Length: > 0 } group
                    ? (Group: group, Source: (Button?)null) : (Group: string.Empty, Source: action.Source))
                .Select(group => group.Count() == 1 ? group.First() : new FeatureAction(
                    group.Key.Group, group.Key.Group + " ▾", group.First().Command, null)
                    { MenuActions = group.ToArray() }).ToArray();
            foreach (var old in _commands) old.Dispose();
            _commands.Clear();
            _commands.AddRange(commands);
        }

        /// <summary>Disposes subscriptions when the workspace closes or cannot resolve a feature.</summary>
        public void ClearActions()
        {
            Actions = Array.Empty<FeatureAction>();
            ToolbarActions = Array.Empty<FeatureAction>();
            foreach (var command in _commands) command.Dispose();
            _commands.Clear();
        }
        #endregion END Public

        #region BEGIN private helpers
        private void OpenActionMenu(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: FeatureAction action } button) return;
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            foreach (var entry in action.MenuActions)
            {
                var item = new MenuItem { Header = entry.Label, Command = entry.Command };
                item.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = entry.Source });
                item.SetBinding(ToolTipProperty, new Binding(nameof(ToolTip)) { Source = entry.Source });
                ToolTipService.SetShowOnDisabled(item, true);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        private static IEnumerable<Button> FindSources(DependencyObject root)
        {
            if (root is ActionBar { IsFeatureActions: true } group)
                foreach (var button in group.Children.OfType<Button>())
                    yield return button;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var button in FindSources(child))
                    yield return button;
        }
        #endregion END private helpers
    }
}
