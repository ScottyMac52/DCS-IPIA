using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
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
            FeatureActionLayoutSmoke.Verify();
            VerifyAssignmentAvailability();
            var module = new DcsModule(Guid.NewGuid(), "ACTION-TEST", "Action test");
            var workspace = new DcsEditorWorkspaceSnapshot(new(module, [], [], [], "smoke"), [], [], [], [], [], [],
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
            var window = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(workspace),
                deviceDiscovery: new EmptyDeviceDiscovery());
            try
            {
                window.Show();
                var bar = (FeatureActionBar)window.FindName("FeatureActions")!;
                WorkspaceUi.Navigate(window, "Devices");
                AssertActions(bar, "Load built-in hardware", "New", "Save to database", "Discard draft",
                    "Import DCS axis defaults…");
                var contentHost = (ContentControl)window.FindName("FeatureContent")!;
                AssertLayoutFits(window, contentHost, bar);
                var oldCommand = bar.Actions.Single(x => x.Label == "New").Command;
                Click(bar, "New");
                var device = (UserControl)((ContentControl)window.FindName("FeatureContent")!).Content;
                if (!((TextBlock)device.FindName("StatusMessage")!).Text.Contains("then Add", StringComparison.Ordinal))
                    throw new InvalidOperationException("The shared action did not invoke the Devices handler.");

                WorkspaceUi.Navigate(window, "Pages");
                AssertLayoutFits(window, contentHost, bar);
                if (oldCommand.CanExecute(null))
                    throw new InvalidOperationException("The inactive feature command was not detached.");
                AssertActions(bar, "New page", "Add missing device pages", "Generate Summary",
                    "Stage page", "Preview draft", "Remove page", "Save to database", "Discard draft");
                Click(bar, "New page");
                var pages = (UserControl)((ContentControl)window.FindName("FeatureContent")!).Content;
                if (((TextBox)pages.FindName("PageFile")!).Text != "overview")
                    throw new InvalidOperationException("The shared action did not invoke the Pages handler.");

                WorkspaceUi.Navigate(window, "Kneeboards");
                AssertLayoutFits(window, contentHost, bar);
                if (bar.Actions.Length != 0 || bar.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("The empty feature retained inactive actions.");
                WorkspaceUi.Navigate(window, "Devices");
                AssertLayoutFits(window, contentHost, bar);
                AssertActions(bar, "Load built-in hardware", "New", "Save to database", "Discard draft",
                    "Import DCS axis defaults…");
            }
            finally { window.Close(); }
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void VerifyAssignmentAvailability()
        {
            VerifyLiveAssignmentToolbar();
            var context = new WorkspaceAssignmentContext("scope", true, false, false, false);
            var control = new HardwareGraphicControl("axis", "Axis", "JOY_X", 0, 0, 100, 30, true);
            var actions = new HardwareDiagramInteractions(_ => { }, CanClear: _ => true, Clear: _ => { },
                CanTune: _ => true, Tune: _ => { }, ActionState: (_, action) =>
                    WorkspaceAssignmentActionState.For(action, context, new(true, true)));
            var button = new Button(); var menu = new ContextMenu();
            foreach (var current in new[] { context, context with { Testing = true }, context with { ReadOnly = true }, context with { Busy = true } })
            {
                context = current;
                AssignmentActionPresentation.Show(button, WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Clear, context, new(true, true)));
                HardwareDiagramPresenter.RefreshMenu(menu, control, actions);
                var clear = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Clear assignment");
                var tune = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Tune axis…");
                if (clear.IsEnabled != button.IsEnabled || tune.IsEnabled != button.IsEnabled || clear.ToolTip as string != button.ToolTip as string)
                    throw new InvalidOperationException("Toolbar and callout menu disagree about the same assignment state.");
                if (!button.IsEnabled && (button.ToolTip is null || !ToolTipService.GetShowOnDisabled(clear)))
                    throw new InvalidOperationException("An unavailable assignment action has no accessible explanation.");
            }
            var unsupported = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.AddAlternative, context, new(false, true));
            var item = new MenuItem(); AssignmentActionPresentation.Show(button, unsupported); AssignmentActionPresentation.Show(item, unsupported);
            if (button.Visibility != Visibility.Collapsed || item.Visibility != Visibility.Collapsed || button.IsEnabled || item.IsEnabled)
                throw new InvalidOperationException("An unsupported action remains advertised by common presentation.");
        }
        private static void VerifyLiveAssignmentToolbar()
        {
            var source = new Button { Name = "Assignment", Content = "Clear assignment" };
            var group = new ActionBar { IsFeatureActions = true }; group.Children.Add(source);
            var bar = new FeatureActionBar(); bar.ShowActions(group);
            var host = new Window { Width = 420, Height = 150, Content = bar, ShowInTaskbar = false };
            try
            {
                host.Show(); ThemeControlSmoke.Pump(host);
                var proxy = FindActionButton(bar) ?? throw new InvalidOperationException("The shared toolbar did not render its action.");
                var blocked = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Clear,
                    new("scope", true, false, false, true), new(true, true));
                AssignmentActionPresentation.Show(source, blocked); ThemeControlSmoke.Pump(host);
                if (proxy.IsEnabled || proxy.ToolTip as string != blocked.Reason || !ToolTipService.GetShowOnDisabled(proxy))
                    throw new InvalidOperationException("The visible toolbar retained a stale availability explanation.");
                AssignmentActionPresentation.Show(source, new(false, false, "unsupported")); ThemeControlSmoke.Pump(host);
                if (proxy.Visibility != Visibility.Collapsed) throw new InvalidOperationException("The visible toolbar retained an unsupported source action.");
                AssignmentActionPresentation.Show(source, new(true, true, "")); ThemeControlSmoke.Pump(host);
                if (!proxy.IsEnabled || proxy.Visibility != Visibility.Visible || proxy.ToolTip is not null)
                    throw new InvalidOperationException("The visible toolbar did not recover from a changed capability state.");
            }
            finally { bar.ClearActions(); host.Close(); }
        }
        private static Button? FindActionButton(DependencyObject root)
        {
            if (root is Button button && button.DataContext is FeatureAction) return button;
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
                if (FindActionButton(System.Windows.Media.VisualTreeHelper.GetChild(root, index)) is { } found) return found;
            return null;
        }
        private static void AssertLayoutFits(Window window, ContentControl content, FeatureActionBar bar)
        {
            ThemeControlSmoke.Pump(window);
            var actionsBottom = bar.TranslatePoint(new Point(0, bar.ActualHeight), window).Y;
            var contentTop = content.TranslatePoint(new Point(), window).Y;
            if (contentTop < actionsBottom - .5 || content.ActualHeight < 100)
                throw new InvalidOperationException("The wrapped action bar overlaps or crushes the feature content.");
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
