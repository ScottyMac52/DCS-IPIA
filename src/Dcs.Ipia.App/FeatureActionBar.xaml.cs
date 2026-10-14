using System.Windows;
using System.Windows.Controls;

namespace DcsIpia.Presentation
{
    /// <summary>Displays stable-position buttons owned by the active workspace feature.</summary>
    public partial class FeatureActionBar : UserControl
    {
        private readonly List<FeatureActionCommand> _commands = [];

        #region BEGIN Public
        public FeatureActionBar()
        {
            InitializeComponent();
        }

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
            var buttons = FindSources(view).ToArray();
            var commands = buttons.Select(button => new FeatureActionCommand(button)).ToArray();
            var next = buttons.Select((button, index) => new FeatureAction(
                button.Name.Length > 0 ? button.Name : "action-" + index,
                button.Content?.ToString() ?? "", commands[index], button.ToolTip)).ToArray();

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
