using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
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
                var oldCommand = bar.Actions.Single(x => x.Label == "New").Command;
                Click(bar, "New");
                var device = (UserControl)((ContentControl)window.FindName("FeatureContent")!).Content;
                if (!((TextBlock)device.FindName("StatusMessage")!).Text.Contains("then Add", StringComparison.Ordinal))
                    throw new InvalidOperationException("The shared action did not invoke the Devices handler.");

                WorkspaceUi.Navigate(window, "Pages");
                if (oldCommand.CanExecute(null))
                    throw new InvalidOperationException("The inactive feature command was not detached.");
                AssertActions(bar, "New page", "Add missing device pages", "Generate Summary",
                    "Stage page", "Preview draft", "Remove page", "Save to database", "Discard draft");
                Click(bar, "New page");
                var pages = (UserControl)((ContentControl)window.FindName("FeatureContent")!).Content;
                if (((TextBox)pages.FindName("PageFile")!).Text != "overview")
                    throw new InvalidOperationException("The shared action did not invoke the Pages handler.");

                WorkspaceUi.Navigate(window, "Kneeboards");
                if (bar.Actions.Count != 0 || bar.Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException("The empty feature retained inactive actions.");
                WorkspaceUi.Navigate(window, "Devices");
                AssertActions(bar, "Load built-in hardware", "New", "Save to database", "Discard draft",
                    "Import DCS axis defaults…");
            }
            finally { window.Close(); }
        }
        #endregion END Public

        #region BEGIN private helpers
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
