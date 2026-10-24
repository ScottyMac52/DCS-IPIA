using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Themed game cards; selecting a card never imports or deploys a profile automatically.</summary>
    public partial class GameSelectionView : UserControl
    {
        private GameSelectionState _recent = new();
        /// <summary>Builds the selection surface.</summary>
        public GameSelectionView() { InitializeComponent(); SetRecent(new()); }
        #region BEGIN Public
        /// <summary>Requests an explicit supported game selection.</summary>
        public event EventHandler<string>? GameRequested;
        /// <summary>Requests reopening the displayed recent workspace.</summary>
        public event EventHandler? ContinueRequested;
        /// <summary>Updates the recent choice without opening it.</summary>
        public void SetRecent(GameSelectionState recent)
        {
            _recent = recent;
            EliteCard.IsEnabled = EditionAccess.Policy.AllowsSimulator("elite-dangerous");
            EliteAccessText.Text = EliteCard.IsEnabled ? "Select controls profile →" : "Requires Premium";
            SquadronsCard.IsEnabled = EditionAccess.Policy.AllowsSimulator("star-wars-squadrons");
            SquadronsAccessText.Text = SquadronsCard.IsEnabled ? "Select controls profile →" : "Requires Premium";
            SquadronsCard.BorderThickness = recent.BackendId == "star-wars-squadrons" ? new Thickness(2) : new Thickness(1);
            ContinueButton.IsEnabled = EditionAccess.Policy.AllowsSimulator(recent.BackendId);
            RecentText.Text = (recent.BackendId switch { "dcs" => "DCS World", "star-wars-squadrons" => "Star Wars: Squadrons", _ => "Elite Dangerous" }) +
                (recent.DisplayName.Length == 0 ? " · Choose a profile" : " · " + recent.DisplayName);
            DcsCard.BorderThickness = recent.BackendId == "dcs" ? new Thickness(2) : new Thickness(1);
            EliteCard.BorderThickness = recent.BackendId == "elite-dangerous" ? new Thickness(2) : new Thickness(1);
        }
        #endregion END Public
        #region BEGIN private helpers
        private void DcsClicked(object sender, RoutedEventArgs e) => GameRequested?.Invoke(this, "dcs");
        private void EliteClicked(object sender, RoutedEventArgs e)
        { if (EditionAccess.Policy.AllowsSimulator("elite-dangerous")) GameRequested?.Invoke(this, "elite-dangerous"); }
        private void SquadronsClicked(object sender, RoutedEventArgs e)
        { if (EditionAccess.Policy.AllowsSimulator("star-wars-squadrons")) GameRequested?.Invoke(this, "star-wars-squadrons"); }
        private void ContinueClicked(object sender, RoutedEventArgs e)
        { if (EditionAccess.Policy.AllowsSimulator(_recent.BackendId)) ContinueRequested?.Invoke(this, EventArgs.Empty); }
        #endregion END private helpers
    }
}
