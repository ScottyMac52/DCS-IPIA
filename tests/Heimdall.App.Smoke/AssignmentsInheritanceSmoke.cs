using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Application;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks panel composition, native callbacks, common mutation gates and notification lifetime with a second consumer.</summary>
    internal static class AssignmentsInheritanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            AssignmentsPanelLayoutSmoke.Verify();
            VerifyAtomicAssignment();
            var source = new AssignmentChangeSource();
            var replacement = new AssignmentChangeSource();
            var view = new AssignmentsBaseProbe();
            var window = new Window { Content = view, Width = 900, Height = 600 };
            try
            {
                view.Bind(source);
                window.Show(); ThemeControlSmoke.Pump(window);
                Require(source.Subscribers == 1, "The base did not attach native state notifications exactly once.");
                Require(IsInside(view.CommandsPanel!, view) && IsInside(view.ControllerPanel!, view) && IsInside(view.EditorPanel!, view),
                    "The base did not compose all three native assignment panels.");
                var before = view.Refreshes;
                view.RefreshAction = source.Raise;
                source.Raise();
                view.RefreshAction = null;
                Require(view.Refreshes == before + 1, "A native state notification did not refresh exactly once, or recursively refreshed the inherited view.");
                view.Bind(replacement);
                Require(source.Subscribers == 0 && replacement.Subscribers == 1, "Rebinding retained an old state source.");
                view.Bind(replacement);
                Require(replacement.Subscribers == 1, "Rebinding duplicated state subscriptions.");
                Require(view.Apply("stale").Outcome == DraftOutcome.Blocked && view.Value == 0, "A stale assignment target mutated the native draft.");
                view.Busy = true;
                Require(view.Apply("probe").Outcome == DraftOutcome.Blocked && view.Value == 0, "A busy native workspace was edited.");
                view.Busy = false;
                Require(view.ApplyAfterReview(() => view.Busy = true).Outcome == DraftOutcome.Blocked && view.Value == 0,
                    "Native review changed availability but the base still applied the assignment.");
                view.Busy = false;
                Require(view.Apply("probe").Outcome == DraftOutcome.Applied && view.Value == 1, "The base did not execute the native action.");
                Require(view.Apply("probe", change: false).Outcome == DraftOutcome.NoChange, "The base reported a no-op as a native change.");
                view.SaveAction = () => throw new InvalidOperationException("native save denial");
                Require(!view.SaveAssignmentsAsync().GetAwaiter().GetResult(), "A rejected native save returned success.");
                Require(view.Status == "Save failed: native save denial" && view.DrawingRefreshes == 0,
                    "A failed native save reported success or refreshed from an unsaved baseline.");
                var pending = new TaskCompletionSource();
                view.SaveAction = () => pending.Task;
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                Task<bool> save;
                try { save = view.SaveAssignmentsAsync(); }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                Require(view.Apply("probe").Outcome == DraftOutcome.Blocked && view.Value == 1,
                    "An inherited save allowed a concurrent assignment mutation.");
                Require(!view.SaveAssignmentsAsync().GetAwaiter().GetResult(), "An overlapping save returned success.");
                Require(!view.DiscardAssignments(), "Discard during persistence returned success.");
                Require(view.Saves == 2 && view.Discards == 0 && view.Status.StartsWith("Discard failed:", StringComparison.Ordinal),
                    "The base allowed overlapping save/discard actions.");
                pending.SetResult();
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!save.IsCompleted)
                {
                    if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("The inherited assignment save did not complete.");
                    ThemeControlSmoke.Pump(window);
                }
                Require(save.GetAwaiter().GetResult(), "A completed native save returned failure.");
                Require(view.Status == "Assignments saved." && view.DrawingRefreshes == 1, "The base did not refresh/report a completed native save.");
                view.DiscardAction = () => throw new InvalidOperationException("native discard denial");
                Require(!view.DiscardAssignments(), "A rejected native discard returned success.");
                Require(view.Status == "Discard failed: native discard denial", "Native discard failure was hidden.");
                view.DiscardAction = () => { };
                Require(view.DiscardAssignments(), "A completed native discard returned failure.");
                Require(view.Status == "Pending edits discarded." && view.DrawingRefreshes == 2, "Native discard did not refresh/report completion.");
                window.Content = null; ThemeControlSmoke.Pump(window);
                Require(replacement.Subscribers == 0, "Unmounting retained the native state subscription.");
                view.Bind(source);
                Require(source.Subscribers == 0, "Rebinding an unmounted view attached state notifications.");
                before = view.Refreshes; replacement.Raise(); source.Raise();
                Require(view.Refreshes == before, "An unmounted assignment view still handled state events.");
                window.Content = view; ThemeControlSmoke.Pump(window);
                Require(source.Subscribers == 1 && view.Refreshes > before, "A cached view did not reattach and refresh on remount.");
            }
            finally { window.Close(); ThemeControlSmoke.Pump(window); view.Dispose(); }
            Require(source.Subscribers == 0, "Closing retained the assignment state subscription.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyAtomicAssignment()
        {
            using var view = new AssignmentsBaseProbe();
            var staleProjectionObserved = false;
            view.RefreshAction = () => staleProjectionObserved |= view.ProjectedValue != view.Value;
            var before = view.Refreshes;
            Require(view.ApplyWithNativeNotification().Outcome == DraftOutcome.Applied, "The native assignment did not apply.");
            Require(!staleProjectionObserved && view.Refreshes == before + 1 && view.ProjectedValue == view.Value,
                "The shared assignment view refreshed before the native projection was rebuilt.");
            before = view.Refreshes;
            try { view.ApplyWithNativeNotification(fail: true); throw new InvalidOperationException("The native failure was swallowed."); }
            catch (InvalidOperationException error) when (error.Message == "native assignment failure") { }
            Require(!staleProjectionObserved && view.Refreshes == before + 1, "A failed assignment retained deferred notifications or refreshed stale rows.");
            before = view.Refreshes;
            Require(view.ApplyWithNativeNotification().Outcome == DraftOutcome.Applied && view.Refreshes == before + 1,
                "The shared assignment notification boundary did not recover after failure.");
        }
        private static bool IsInside(DependencyObject child, DependencyObject ancestor)
        {
            for (DependencyObject? item = child; item is not null; item = VisualTreeHelper.GetParent(item))
                if (ReferenceEquals(item, ancestor)) return true;
            return false;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        #endregion END private helpers
    }
}
