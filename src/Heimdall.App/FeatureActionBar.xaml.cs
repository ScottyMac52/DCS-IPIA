using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App
{
    /// <summary>Displays wrapping, content-sized buttons owned by the active workspace feature.</summary>
    public partial class FeatureActionBar : UserControl
    {
        private readonly List<FeatureActionCommand> _commands = [];

        #region BEGIN Public
        public FeatureActionBar()
        {
            InitializeComponent();
        }

        /// <summary>Optional workspace-specific toolbar style; button height remains content-driven.</summary>
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
            foreach (var old in _commands) old.Dispose();
            _commands.Clear();
            _commands.AddRange(commands);
        }

        /// <summary>Disposes subscriptions when the workspace closes or cannot resolve a feature.</summary>
        public void ClearActions()
        {
            Actions = Array.Empty<FeatureAction>();
            foreach (var command in _commands) command.Dispose();
            _commands.Clear();
        }
        #endregion END Public

        /// <summary>Shows one direct action from two mutually exclusive states.</summary>
        internal static void ShowAlternate(Button primary, Button alternate, bool useAlternate)
        {
            primary.Visibility = useAlternate ? Visibility.Collapsed : Visibility.Visible;
            alternate.Visibility = useAlternate ? Visibility.Visible : Visibility.Collapsed;
        }

        #region BEGIN private helpers
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
