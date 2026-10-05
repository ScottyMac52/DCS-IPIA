using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Verifies input-test log selection, clipboard export and clearing without losing transitions.</summary>
    public static class InputTestLogSmoke
    {
        #region BEGIN Public
        /// <summary>Checks selected/all copy, failure handling and clearing during active acquisition.</summary>
        /// <param name="window">Visible module editor with an active input test.</param>
        /// <param name="source">Deterministic device source.</param>
        /// <param name="clipboard">Returns the captured clipboard payload.</param>
        /// <param name="setFailure">Controls a simulated clipboard failure.</param>
        public static void VerifyLive(ModuleWorkspaceWindow window, FakeDeviceInputSource source,
            Func<InputTestLogClipboard?> clipboard, Action<bool> setFailure)
        {
            var log = (ListBox)window.FindName("InputTestLog")!;
            var clear = (Button)window.FindName("ClearInputTestLogButton")!;
            var copy = (Button)window.FindName("CopyInputTestLogButton")!;
            var message = (TextBlock)window.FindName("InputTestLogMessage")!;
            var stop = (Button)window.FindName("StopInputTestButton")!;
            var first = log.Items.Cast<InputTestLogEntry>().Single();
            var second = first with { Commands = "Second <event> & é" };
            var third = first with { Commands = "Third" };
            log.Items.Add(second);
            log.Items.Add(third);
            if (log.SelectionMode != SelectionMode.Extended) throw new InvalidOperationException("Event log does not support multi-selection.");
            log.SelectedItems.Clear();
            Click(copy);
            if (clipboard()?.Text.Split("\r\n").Length != 4 || !clipboard()!.Html.Contains("<table", StringComparison.Ordinal) ||
                !clipboard()!.Html.Contains("Second &lt;event&gt; &amp; é", StringComparison.Ordinal))
                throw new InvalidOperationException("Copy-all did not produce an escaped event table.");
            log.SelectedItems.Add(second);
            Click(copy);
            if (clipboard()?.Text.Split("\r\n").Length != 2 || !clipboard()!.Text.EndsWith(second.Commands, StringComparison.Ordinal))
                throw new InvalidOperationException("Single-row copy included unselected events.");
            log.SelectedItems.Clear();
            log.SelectedItems.Add(third);
            log.SelectedItems.Add(first);
            Click(copy);
            var selectedText = clipboard()!.Text.Split("\r\n");
            if (selectedText.Length != 3 || !selectedText[1].EndsWith(first.Commands, StringComparison.Ordinal) || !selectedText[2].EndsWith("Third", StringComparison.Ordinal))
                throw new InvalidOperationException("Multi-selection was not copied in displayed order.");
            setFailure(true);
            Click(copy);
            setFailure(false);
            if (!message.Text.StartsWith("Could not copy events", StringComparison.Ordinal) || source.Disposed || !stop.IsEnabled || log.Items.Count != 3)
                throw new InvalidOperationException("Clipboard failure stopped testing or changed its log.");
            Click(clear);
            Drain(window);
            if (log.HasItems || log.SelectedItems.Count != 0 || clear.IsEnabled || copy.IsEnabled || source.Disposed || !stop.IsEnabled)
                throw new InvalidOperationException("Clear did not empty the log while retaining acquisition.");
            source.ButtonValue = 0;
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!log.HasItems && DateTime.UtcNow < deadline) Drain(window);
            var release = log.Items.Cast<InputTestLogEntry>().SingleOrDefault();
            if (release is null || release.Transition != "Release" || !release.Commands.Contains("Test action (KEY)", StringComparison.Ordinal))
                throw new InvalidOperationException("Clear reset the baseline or lost the pressed command before release.");
        }
        /// <summary>Checks that stopped logs can still be copied and cleared, with disabled empty-log actions.</summary>
        /// <param name="window">Module editor after Stop.</param>
        /// <param name="clipboard">Returns the captured clipboard payload.</param>
        public static void VerifyStopped(ModuleWorkspaceWindow window, Func<InputTestLogClipboard?> clipboard)
        {
            var log = (ListBox)window.FindName("InputTestLog")!;
            var clear = (Button)window.FindName("ClearInputTestLogButton")!;
            var copy = (Button)window.FindName("CopyInputTestLogButton")!;
            Click(copy);
            if (clipboard()?.Text.Split("\r\n").Length != 2) throw new InvalidOperationException("Stopped log was not copied.");
            Click(clear);
            Drain(window);
            if (log.HasItems || clear.IsEnabled || copy.IsEnabled) throw new InvalidOperationException("Stopped log was not cleared.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static void Drain(Window window) => window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        #endregion END private helpers
    }
}
