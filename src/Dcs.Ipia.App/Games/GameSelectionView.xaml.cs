using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application.Games;

namespace DcsIpia.Presentation.Games
{
    /// <summary>Themed game cards; selecting a card never imports or deploys a profile automatically.</summary>
    public partial class GameSelectionView : UserControl
    {
        private GameSelectionState _recent = new();
        /// <summary>Builds the selection surface.</summary>
        public GameSelectionView() => InitializeComponent();
        #region BEGIN Public
        /// <summary>Requests an explicit supported game selection.</summary>
        public event EventHandler<string>? GameRequested;
        /// <summary>Requests reopening the displayed recent workspace.</summary>
        public event EventHandler? ContinueRequested;
        /// <summary>Updates the recent choice without opening it.</summary>
        public void SetRecent(GameSelectionState recent)
        {
            _recent = recent;
            RecentText.Text = (recent.BackendId == "dcs" ? "DCS World" : "Elite Dangerous") +
                (recent.DisplayName.Length == 0 ? " · Choose a profile" : " · " + recent.DisplayName);
            DcsCard.BorderThickness = recent.BackendId == "dcs" ? new Thickness(2) : new Thickness(1);
            EliteCard.BorderThickness = recent.BackendId == "elite-dangerous" ? new Thickness(2) : new Thickness(1);
        }
        #endregion END Public
        #region BEGIN private helpers
        private void DcsClicked(object sender, RoutedEventArgs e) => GameRequested?.Invoke(this, "dcs");
        private void EliteClicked(object sender, RoutedEventArgs e) => GameRequested?.Invoke(this, "elite-dangerous");
        private void ContinueClicked(object sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
        #endregion END private helpers
    }
}
