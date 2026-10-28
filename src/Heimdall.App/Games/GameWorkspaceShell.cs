using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Games
{
    /// <summary>
    /// Composed workspace host for profile chrome, navigation, an optional separate
    /// action row, native feature content and status. Layout lives here rather than
    /// in each game window.
    /// This is deliberately a Grid, not a new game-specific mega-window.
    /// </summary>
    public sealed class GameWorkspaceShell : Grid
    {
        #region BEGIN Public
        /// <summary>Selects a dedicated feature-action row for hosts with separate navigation and action bars.</summary>
        public static readonly DependencyProperty SeparateFeatureActionsProperty = DependencyProperty.Register(
            nameof(SeparateFeatureActions), typeof(bool), typeof(GameWorkspaceShell),
            new PropertyMetadata(false, (owner, _) => ((GameWorkspaceShell)owner).BuildRows()));
        /// <summary>Gets or sets whether navigation and feature actions occupy separate shell rows.</summary>
        public bool SeparateFeatureActions
        {
            get => (bool)GetValue(SeparateFeatureActionsProperty);
            set => SetValue(SeparateFeatureActionsProperty, value);
        }
        /// <summary>Creates the common workspace layout without owning simulator behavior.</summary>
        public GameWorkspaceShell() => BuildRows();
        #endregion END Public
        #region BEGIN private helpers
        private void BuildRows()
        {
            RowDefinitions.Clear();
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (SeparateFeatureActions) RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        #endregion END private helpers
    }
}
