using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises the modal command-import path while the shared scan gate is deliberately held.</summary>
    internal static class DcsCommandImportStateSmoke
    {
        #region BEGIN Public
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
            VerifyLiveDraft(window, manager, status);
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void VerifyLiveDraft(DcsModuleWorkspaceWindow window, UserControl manager, TextBlock status)
        {
            var context = window.DcsContext;
            var original = context.Working!;
            var originalAssignments = context.Draft!.Snapshot;
            var labels = new Dictionary<string, string>(context.Labels, StringComparer.Ordinal);
            var saved = JsonSerializer.Serialize(context.Saved);
            var profile = original.Configuration.Profiles[0];
            var command = original.ModuleCommands.Single(command => command.Id == "KEY");
            var slot = new DcsDraftAssignmentSlot(original.Configuration.Module.Id, profile.Id,
                AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            try
            {
                Require(context.Draft!.Assign(slot, new(command.Id, command.Name, false, AssignmentLayer.Module),
                    OccupiedDestinationBehavior.ReplaceInDraft).Outcome == DraftOutcome.Applied,
                    "The pending-assignment import fixture could not stage an assignment.");
                foreach (var caption in new[] { "Import JSON…", "Import HTML…" })
                {
                    var count = context.Working!.ModuleCommands.Count;
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var review = System.Windows.Application.Current.Windows.OfType<DcsCommandImportReviewWindow>().Single();
                        ((Button)review.FindName("StageImportButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                    WorkspaceUi.Button(manager, caption).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(context.Working!.ModuleCommands.Count == count + 1 &&
                        context.Draft!.Snapshot.Any(assignment => assignment.Slot == slot && assignment.Command.Id == command.Id),
                        "Import rejected the live draft or lost a pending assignment: " + status.Text);
                    Require(JsonSerializer.Serialize(context.Saved) == saved, "Staging command import changed the saved baseline.");
                }

                var beforeStale = JsonSerializer.Serialize(context.Working);
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Require(context.Draft!.Remove(slot).Outcome == DraftOutcome.Applied,
                        "The modal stale-review fixture could not change the assignment draft.");
                    var review = System.Windows.Application.Current.Windows.OfType<DcsCommandImportReviewWindow>().Single();
                    ((Button)review.FindName("StageImportButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                WorkspaceUi.Button(manager, "Import JSON…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(status.Text == "The module changed after this command review. Load and review the files again." &&
                    JsonSerializer.Serialize(context.Working) == beforeStale &&
                    !context.Draft!.Snapshot.Any(assignment => assignment.Slot == slot),
                    "A genuinely stale import applied or discarded the newer draft edit.");
            }
            finally
            {
                Require(context.RunStructureEdit(_ => original with { Assignments = originalAssignments, Labels = labels }).Outcome == WorkspaceOperationOutcome.Applied,
                    "The command-import smoke fixture could not restore its original staged catalog.");
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        #endregion END private helpers
    }
}
