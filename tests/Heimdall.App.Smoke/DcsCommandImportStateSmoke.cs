using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises the modal command-import path while the shared scan gate is deliberately held.</summary>
    internal static class DcsCommandImportStateSmoke
    {
        internal static void Verify(DcsModuleWorkspaceWindow window)
        {
            var feature = WorkspaceUi.Navigate(window, "Commands");
            var manager = (UserControl)feature.FindName("Manager");
            var import = WorkspaceUi.Button(manager, "Import JSON…");
            var status = (TextBlock)manager.FindName("StatusText");
            var context = window.DcsContext;
            var before = context.CaptureSnapshot();
            Require(context.TryStartActivity(WorkspaceActivityKind.Scan, out var scan).Enabled, "The controlled discovery scan was not admitted.");
            using (scan!)
            {
                Require(!((ContentControl)window.FindName("FeatureContent")).IsEnabled &&
                    !((FeatureActionBar)window.FindName("FeatureActions")).IsEnabled,
                    "DCS authoring remained enabled during controller discovery.");
                var invoked = false;
                var blocked = context.RunStructureEdit(current => { invoked = true; return current; });
                Require(blocked.Outcome == WorkspaceOperationOutcome.Blocked && !invoked && blocked.Message.Length > 0,
                    "A rejected structural edit ran or lost its blocked result.");
                // Routed events deliberately bypass disabled UI, proving the underlying modal path also respects the gate.
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var review = System.Windows.Application.Current.Windows.OfType<DcsCommandImportReviewWindow>().Single();
                    ((Button)review.FindName("StageImportButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                import.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(context.CaptureSnapshot() == before && status.Text == blocked.Message,
                    "An import during scanning changed the draft or reported a blocked import as successful.");
            }
            Require(((ContentControl)window.FindName("FeatureContent")).IsEnabled &&
                ((FeatureActionBar)window.FindName("FeatureActions")).IsEnabled, "Discovery completion did not re-enable authoring.");
            var rejection = context.RunStructureEdit(_ => throw new InvalidOperationException("Synthetic native rejection"));
            Require(rejection.Outcome == WorkspaceOperationOutcome.Blocked && rejection.Message == "Synthetic native rejection" &&
                context.CaptureSnapshot() == before && context.State.Operation is null,
                "Native structural rejection was swallowed, changed the draft, or retained the operation gate.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
