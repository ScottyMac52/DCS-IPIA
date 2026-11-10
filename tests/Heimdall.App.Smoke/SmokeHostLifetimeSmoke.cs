using System.Windows;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that closing the last fixture window cannot terminate the shared smoke host.</summary>
    internal static class SmokeHostLifetimeSmoke
    {
        #region BEGIN Public
        internal static void Verify(System.Windows.Application application)
        {
            var first = new Window(); first.Show(); ThemeControlSmoke.Pump(first); first.Close();
            var next = new Window();
            try
            {
                next.Show(); ThemeControlSmoke.Pump(next);
                if (!next.IsVisible || application.Dispatcher.HasShutdownStarted || application.Dispatcher.HasShutdownFinished)
                    throw new InvalidOperationException("Closing a fixture terminated the smoke host before the next scenario.");
            }
            finally { next.Close(); }
        }
        #endregion END Public
    }
}
