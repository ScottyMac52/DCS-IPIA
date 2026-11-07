using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises inherited algorithms against injected non-DCS actions and lifecycle failures.</summary>
    internal static class WorkspaceInheritanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var window = new WorkspaceBaseProbe();
            try
            {
                window.Show();
                window.NavigateToFeature("First");
                var first = window.Surface.Content;
                window.NavigateToFeature("Second");
                window.NavigateToFeature("First");
                Require(ReferenceEquals(first, window.Surface.Content) && window.FirstCreations == 1,
                    "The inherited navigation algorithm did not cache the feature.");
                window.SecondAvailable = false;
                window.NavigateToFeature("Second");
                Require(window.Surface.Content is TextBlock { Text: "Feature unavailable" }, "Cached feature availability was not rechecked.");
                window.SecondAvailable = true;
                window.NavigateToFeature("Second");
                Require(window.Surface.Content is UserControl, "An available feature did not remount.");
                window.NavigateToFeature("First");

                window.Pending = true; window.RefreshChrome();
                Require(window.Save.IsEnabled && window.Discard.IsEnabled, "Base pending state did not enable common commands.");
                Require(!window.TryLeaveAsync(GameWorkspaceLeaveDecision.Cancel).GetAwaiter().GetResult() && window.Pending,
                    "Cancel changed pending state or allowed a switch.");
                window.Close();
                Require(window.IsVisible && window.CleanupCount == 0, "A canceled close released the workspace.");

                window.SaveAvailable = false; window.RefreshChrome();
                Require(!window.Save.IsEnabled, "Unavailable native save left the common button enabled.");
                var denial = new InvalidOperationException("Native policy denies saving this workspace.");
                window.SaveAction = () => throw denial;
                Exception? rejection = null;
                try { window.SaveWorkspaceAsync().GetAwaiter().GetResult(); }
                catch (InvalidOperationException error) { rejection = error; }
                Require(rejection?.Message == "Native save is unavailable." && window.Pending && !window.Save.IsEnabled,
                    "The shared context failed to reject native ineligibility before invoking persistence.");
                window.SaveAvailable = true; window.RefreshChrome();

                window.SaveAction = () => throw new InvalidOperationException("Expected persistence failure");
                Require(!window.TryLeaveAsync(GameWorkspaceLeaveDecision.Save).GetAwaiter().GetResult() && window.Pending && window.Error is not null,
                    "A failed adapter save lost edits or allowed a switch.");
                Require(window.Save.IsEnabled && window.Discard.IsEnabled, "Failure did not restore base command availability.");

                var pendingSave = new TaskCompletionSource();
                var saves = 0;
                window.SaveAction = async () => { saves++; await pendingSave.Task; window.Pending = false; };
                var save = window.SaveWorkspaceAsync();
                Require(!window.Save.IsEnabled && !window.Discard.IsEnabled, "Save did not enter the inherited busy state.");
                Reject(() => window.SaveWorkspaceAsync().GetAwaiter().GetResult());
                Reject(() => window.DiscardClicked(window, new()));
                Require(saves == 1, "The base allowed overlapping persistence operations.");
                Require(!window.TryLeaveAsync(GameWorkspaceLeaveDecision.Discard).GetAwaiter().GetResult(), "Switching was allowed during persistence.");
                window.Close();
                Require(window.IsVisible, "Closing was allowed during persistence.");
                pendingSave.SetResult();
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!save.IsCompleted)
                {
                    if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Inherited save did not complete.");
                    ThemeControlSmoke.Pump(window);
                }
                save.GetAwaiter().GetResult();
                Require(!window.Pending && !window.Save.IsEnabled && !window.Discard.IsEnabled, "Successful save did not refresh common state.");

                window.Pending = true; window.RefreshChrome();
                var discards = 0;
                window.DiscardAction = () => { discards++; window.Pending = false; };
                window.Discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(discards == 1 && !window.Pending, "The inherited Discard handler did not invoke the adapter hook.");
                window.Pending = true; window.RefreshChrome();
                window.SaveAction = () => { saves++; window.Pending = false; return Task.CompletedTask; };
                window.Save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(saves == 2 && !window.Pending, "The inherited Save handler did not invoke the adapter hook.");
                window.Pending = true;
                Require(window.TryLeaveAsync(GameWorkspaceLeaveDecision.Discard).GetAwaiter().GetResult() && discards == 2,
                    "The base switch guard did not invoke Discard.");
                window.Pending = true;
                Require(window.TryLeaveAsync(GameWorkspaceLeaveDecision.Save).GetAwaiter().GetResult() && saves == 3,
                    "The base switch guard did not invoke Save.");
                window.Close();
                Require(window.CleanupCount == 1 && window.Surface.Content is null, "Successful close did not release owned features and resources.");
                Reject(() => window.SaveWorkspaceAsync().GetAwaiter().GetResult());
                Reject(() => window.DiscardClicked(window, new()));
                window.NavigateToFeature("First");
                Require(window.Surface.Content is null && window.FirstCreations == 1, "Closed navigation recreated a feature.");
            }
            finally { window.Pending = false; window.Close(); }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void Reject(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("A rejected workspace action incorrectly completed successfully.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        #endregion END private helpers
    }
}
