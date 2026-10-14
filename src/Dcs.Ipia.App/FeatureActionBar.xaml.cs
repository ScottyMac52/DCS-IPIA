using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace DcsIpia.Presentation
{
    /// <summary>Hosts consistent, navigation-aware feature buttons without feature business logic.</summary>
    public partial class FeatureActionBar : UserControl
    {
        private readonly List<FeatureActionCommand> _commands = [];

        #region BEGIN Public
        public FeatureActionBar()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        /// <summary>Active view's action descriptors; empty when no actions are supplied.</summary>
        public ObservableCollection<FeatureAction> Actions { get; } = [];

        /// <summary>Presents the active view's explicitly marked actions in declaration order.</summary>
        public void ShowActions(DependencyObject view)
        {
            ArgumentNullException.ThrowIfNull(view);
            ClearActions();
            var index = 0;
            foreach (var button in FindSources(view))
            {
                var command = new FeatureActionCommand(button);
                _commands.Add(command);
                Actions.Add(new FeatureAction(button.Name.Length > 0 ? button.Name : "action-" + index++,
                    button.Content?.ToString() ?? "", command, button.ToolTip));
            }
            Visibility = Actions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Releases stale commands and availability subscriptions.</summary>
        public void ClearActions()
        {
            Actions.Clear();
            foreach (var command in _commands) command.Dispose();
            _commands.Clear();
            Visibility = Visibility.Collapsed;
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
