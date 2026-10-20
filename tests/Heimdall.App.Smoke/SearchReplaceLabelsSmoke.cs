using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises replacement preview validity and regex failures through the real modal dialog.</summary>
    public static class SearchReplaceLabelsSmoke
    {
        #region BEGIN Public
        /// <summary>Checks stale previews cannot apply, timeouts discard partial results, and valid replacements still work.</summary>
        public static void Verify()
        {
            var module = new DcsModule(Guid.NewGuid(), "REPLACE", "Label replacement regression");
            var profile = new DeviceProfile(Guid.NewGuid(), "tm-tpr", null, "Rudder", "Rudder.diff.lua");
            var first = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X");
            var second = first with { InputKey = "JOY_Y" };
            var graphic = new UiLayerDiagram("Rudder", null, "rudder.svg", 240, 180, [], [])
            {
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 240 180'><rect width='240' height='180' fill='black'/></svg>",
                Controls = [new("x", "X", "JOY_X", 20, 20, 200, 50, true), new("y", "Y", "JOY_Y", 20, 90, 200, 50, true)]
            };
            var workspace = new EditorWorkspaceSnapshot(new(module, [profile], [], [], "smoke"), [], [], [graphic],
                [new("X", "Alpha 12", "Axis", true), new("Y", "Alpha 34", "Axis", true)], [],
                [new(first, new("X", "Alpha 12", true, AssignmentLayer.Module)),
                 new(second, new("Y", "Alpha 34", true, AssignmentLayer.Module))],
                new Dictionary<string, string>(), new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace),
                deviceDiscovery: new EmptyDeviceDiscovery(), persistLayout: false);
            var context = new WindowContext(window);
            try
            {
                window.Show();
                var view = WorkspaceUi.Navigate(window, "Assignments");
                var open = ActionBar.FindButton(view, "Search & replace labels…")!;
                DriveDialog(window, open, dialog =>
                {
                    var panel = (Panel)dialog.Content;
                    var boxes = panel.Children.OfType<TextBox>().ToArray();
                    var regex = panel.Children.OfType<CheckBox>().Single();
                    var preview = panel.Children.OfType<ListBox>().Single();
                    var apply = panel.Children.OfType<StackPanel>().Last().Children.OfType<Button>().First();
                    AssertInvalid(preview, apply);
                    boxes[1].Text = "Beta";
                    boxes[0].Text = "Alpha";
                    AssertMatches(preview, apply, 2);
                    boxes[0].Text = "";
                    AssertInvalid(preview, apply);
                    Click(apply); // Even a directly raised click must revalidate before changing labels.
                    if (context.Labels.Count != 0 || !dialog.IsVisible)
                        throw new InvalidOperationException("An empty search applied a stale preview.");
                    boxes[0].Text = "Alpha";
                    AssertMatches(preview, apply, 2);
                    regex.IsChecked = true;
                    boxes[0].Text = "[";
                    AssertInvalid(preview, apply);
                    Click(apply);
                    if (context.Labels.Count != 0 || !dialog.IsVisible)
                        throw new InvalidOperationException("An invalid regex applied a stale preview.");
                    regex.IsChecked = false;
                    boxes[0].Text = "missing";
                    AssertInvalid(preview, apply);
                    boxes[0].Text = "alpha";
                    AssertMatches(preview, apply, 2);
                    Click(apply);
                });
                if (context.Labels[first.Key] != "Beta 12" || context.Labels[second.Key] != "Beta 34")
                    throw new InvalidOperationException("Case-insensitive literal replacement failed.");

                DriveDialog(window, open, dialog =>
                {
                    var panel = (Panel)dialog.Content;
                    var boxes = panel.Children.OfType<TextBox>().ToArray();
                    boxes[1].Text = "$2 $1";
                    panel.Children.OfType<CheckBox>().Single().IsChecked = true;
                    boxes[0].Text = @"(Beta) (\d+)";
                    Click(panel.Children.OfType<StackPanel>().Last().Children.OfType<Button>().First());
                });
                if (context.Labels[first.Key] != "12 Beta" || context.Labels[second.Key] != "34 Beta")
                    throw new InvalidOperationException("Regex replacement groups failed.");

                context.SetLabel(first.Key, "Alpha");
                context.SetLabel(second.Key, new string('a', 10000) + "!");
                DriveDialog(window, open, dialog =>
                {
                    var panel = (Panel)dialog.Content;
                    var boxes = panel.Children.OfType<TextBox>().ToArray();
                    boxes[1].Text = "Changed";
                    boxes[0].Text = "Alpha";
                    var preview = panel.Children.OfType<ListBox>().Single();
                    var apply = panel.Children.OfType<StackPanel>().Last().Children.OfType<Button>().First();
                    AssertMatches(preview, apply, 1);
                    panel.Children.OfType<CheckBox>().Single().IsChecked = true;
                    boxes[0].Text = @"Alpha|^(a+)+$";
                    AssertInvalid(preview, apply);
                    if (!panel.Children.OfType<TextBlock>().Last().Text.Contains("timed out", StringComparison.Ordinal))
                        throw new InvalidOperationException("Regex timeout did not produce a recoverable message.");
                    Click(apply);
                    if (context.Labels[first.Key] != "Alpha" || !dialog.IsVisible)
                        throw new InvalidOperationException("Regex timeout applied partial replacements.");
                    boxes[0].Text = "Alpha";
                    AssertMatches(preview, apply, 1);
                    Click(panel.Children.OfType<StackPanel>().Last().Children.OfType<Button>().Last());
                });
                if (context.Labels[first.Key] != "Alpha")
                    throw new InvalidOperationException("Cancel applied a replacement.");
            }
            finally { context.Discard(); window.Close(); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void DriveDialog(Window owner, Button open, Action<Window> review)
        {
            Exception? failure = null;
            var handled = false;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                Window? dialog = null;
                try
                {
                    dialog = owner.OwnedWindows.Cast<Window>().Single(w => w.Title == "Search & replace labels");
                    review(dialog);
                    handled = true;
                }
                catch (Exception ex) { failure = ex; }
                finally { if (dialog?.IsVisible == true) dialog.Close(); }
            }));
            Click(open);
            if (failure is not null) throw new InvalidOperationException("Search/replace dialog regression.", failure);
            if (!handled) throw new InvalidOperationException("Search/replace dialog did not open.");
        }
        private static void AssertInvalid(ListBox preview, Button apply)
        {
            if (preview.Items.Count != 0 || apply.IsEnabled)
                throw new InvalidOperationException("Invalid or empty preview retained applicable replacements.");
        }
        private static void AssertMatches(ListBox preview, Button apply, int count)
        {
            if (preview.Items.Count != count || !apply.IsEnabled)
                throw new InvalidOperationException("Valid replacement preview was unavailable.");
        }
        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        #endregion END private helpers
    }
}
