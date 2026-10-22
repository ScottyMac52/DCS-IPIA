using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks simulator cards, direct shell construction and retained data across edition changes.</summary>
    internal static class SimulatorEditionSmoke
    {
        #region BEGIN Public
        /// <summary>Blocks Free ED entry before input acquisition and reopens the retained profile as Owner.</summary>
        internal static void Verify(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware, Guid id, OwnerLicenseTestIssuer issuer)
        {
            var before = JsonSerializer.Serialize(services.Store.Load(id));
            EditionAccess.SetForTests(new());
            var cards = new GameSelectionView();
            cards.SetRecent(new("elite-dangerous", id, DisplayName: "Retained Premium profile"));
            var requested = ""; var continued = false;
            cards.GameRequested += (_, game) => requested = game; cards.ContinueRequested += (_, _) => continued = true;
            var elite = (Button)cards.FindName("EliteCard"); var resume = (Button)cards.FindName("ContinueButton");
            if (elite.IsEnabled || resume.IsEnabled || ((TextBlock)cards.FindName("EliteAccessText")).Text != "Requires Premium")
                throw new InvalidOperationException("Free cards did not explain and disable reserved simulator entry.");
            elite.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); resume.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (requested.Length != 0 || continued) throw new InvalidOperationException("Direct card events bypassed the simulator entitlement.");
            ((Button)cards.FindName("DcsCard")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (requested != "dcs") throw new InvalidOperationException("Free lost DCS access.");
            var acquired = false;
            try
            {
                using var unexpected = new EliteWorkspaceWindow(services, hardware, id,
                    inputFactory: (_, _) => { acquired = true; throw new InvalidOperationException("Unexpected native acquisition."); });
                throw new InvalidOperationException("Free constructed the ED workspace.");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("requires HEIMDALL Premium", StringComparison.Ordinal)) { }
            if (acquired || JsonSerializer.Serialize(services.Store.Load(id)) != before)
                throw new InvalidOperationException("Blocked ED entry acquired input or modified the saved profile.");
            EditionAccess.SetForTests(issuer.Policy);
            using var owner = new EliteWorkspaceWindow(services, hardware, id);
            owner.Show(); ThemeControlSmoke.Pump(owner);
            if (owner.Session?.Snapshot.Id != id || owner.Session.AccessMessage.Length != 0)
                throw new InvalidOperationException("Owner could not reopen the retained ED configuration.");
            owner.Close();
        }
        #endregion END Public
    }
}
