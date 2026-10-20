using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that navigation replaces shell actions without stealing view behavior.</summary>
    public static class FeatureActionBarSmoke
    {
        #region BEGIN Public
        /// <summary>Exercises real action sets, original callbacks, and inactive command disposal.</summary>
        public static void Verify()
        {
            var module = new DcsModule(Guid.NewGuid(), "ACTION-TEST", "Action test");
            var workspace = new EditorWorkspaceSnapshot(new(module, [], [], [], "smoke"), [], [], [], [], [], [],
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace),
                deviceDiscovery: new EmptyDeviceDiscovery());
            try
            {
                window.Show();
                var bar = (FeatureActionBar)window.FindName("FeatureActions")!;
                WorkspaceUi.Navigate(window, "Devices");
                AssertActions(bar, "Load built-in hardware", "New", "Save to database", "Discard draft",
                    "Import DCS axis defaults…");
                var contentHost = (ContentControl)window.FindName("FeatureContent")!;
                var scroll = (ScrollViewer)bar.FindName("ActionScroll")!;
                if (scroll.HorizontalScrollBarVisibility != ScrollBarVisibility.Auto ||
                    scroll.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                    throw new InvalidOperationException("The feature action strip should scroll horizontally, not wrap.");
                var top = contentHost.TranslatePoint(new Point(), window).Y;
                var height = contentHost.ActualHeight;
                var oldCommand = bar.Actions.Single(x => x.Label == "New").Command;
                Click(bar, "New");
                var device = (UserControl)((ContentControl)window.FindName("FeatureContent")!).Content;
                if (!((TextBlock)device.FindName("StatusMessage")!).Text.Contains("then Add", StringComparison.Ordinal))
                    throw new InvalidOperationException("The shared action did not invoke the Devices handler.");

                WorkspaceUi.Navigate(window, "Pages");
                AssertLayoutStable(window, contentHost, top, height);
                if (oldCommand.CanExecute(null))
                    throw new InvalidOperationException("The inactive feature command was not detached.");
                AssertActions(bar, "New page", "Add missing device pages", "Generate Summary",
                    "Stage page", "Preview draft", "Remove page", "Save to database", "Discard draft");
                Click(bar, "New page");
                var pages = (UserControl)((ContentControl)window.FindName("FeatureContent")!).Content;
                if (((TextBox)pages.FindName("PageFile")!).Text != "overview")
                    throw new InvalidOperationException("The shared action did not invoke the Pages handler.");

                WorkspaceUi.Navigate(window, "Kneeboards");
                AssertLayoutStable(window, contentHost, top, height);
                if (bar.Actions.Length != 0 || bar.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("The empty feature retained inactive actions.");
                WorkspaceUi.Navigate(window, "Devices");
                AssertLayoutStable(window, contentHost, top, height);
                AssertActions(bar, "Load built-in hardware", "New", "Save to database", "Discard draft",
                    "Import DCS axis defaults…");
            }
            finally { window.Close(); }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void AssertLayoutStable(Window window, ContentControl content, double top, double height)
        {
            window.UpdateLayout();
            var currentTop = content.TranslatePoint(new Point(), window).Y;
            if (Math.Abs(currentTop - top) > 0.5 || Math.Abs(content.ActualHeight - height) > 0.5)
                throw new InvalidOperationException(
                    $"Navigation shifted the content host: top {top} to {currentTop}, height {height} to {content.ActualHeight}.");
        }

        private static void AssertActions(FeatureActionBar bar, params string[] expected)
        {
            if (!bar.Actions.Select(x => x.Label).SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidOperationException("Unexpected feature action set: " +
                    string.Join(", ", bar.Actions.Select(x => x.Label)));
        }

        private static void Click(FeatureActionBar bar, string label)
        {
            var action = bar.Actions.Single(x => x.Label == label);
            if (!action.Command.CanExecute(null))
                throw new InvalidOperationException("Action unexpectedly disabled: " + label);
            action.Command.Execute(null);
        }
        #endregion END private helpers
    }
}
