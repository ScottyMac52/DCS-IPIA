using System.Windows;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks the context-to-WPF refresh boundary against all four production adapters.</summary>
    internal static class WorkspacePresentationConformanceSmoke
    {
        internal static void Verify(WorkspaceContextCase fixture)
        {
            var context = fixture.Context;
            var view = new ContextAssignmentsProbe(fixture);
            var host = new ContextWindowProbe(fixture, view);
            try
            {
                host.Show(); ThemeControlSmoke.Pump(host);
                var name = context.State.EditedName;
                var snapshot = fixture.NativeSnapshot();
                view.Pending.Text = "Pending editor input";
                var reloads = view.Reloads; var availability = view.AvailabilityUpdates;
                // This must work on the first notification after mounting, not only once observation warms up.
                context.Select(context.State.Selection with { CommandId = "command", InputId = "pending" });
                context.SetEditedName(name + " pending");
                if (context.TryStartActivity(WorkspaceActivityKind.Test, out var input).Enabled) input!.Dispose();
                Require(context.TryStartActivity(WorkspaceActivityKind.Scan, out var old).Enabled, fixture, "Scan was unavailable.");
                Require(context.TryStartActivity(WorkspaceActivityKind.Scan, out var current).Enabled, fixture, "Newer scan was rejected.");
                old!.Dispose(); current!.Dispose();
                try
                {
                    context.RunOperation(WorkspaceOperationKind.Save, _ => throw new System.IO.IOException("Synthetic failed save"));
                    throw new InvalidOperationException("Expected save failure.");
                }
                catch (System.IO.IOException) { }
                ThemeControlSmoke.Pump(host);
                Require(view.Reloads == reloads && view.Pending.Text == "Pending editor input" && view.AvailabilityUpdates > availability,
                    fixture, "Selection, name, input gates or failed-save notifications reloaded a pending editor instead of updating availability.");
                Require(fixture.NativeSnapshot() == snapshot && context.State.EditedName == name + " pending", fixture, "Presentation transitions changed native data or lost the pending name.");
                context.SetEditedName(name);
                fixture.Edit("Changed native record");
                Require(view.Reloads == reloads + 1 && view.Pending.Text == "Loaded native record", fixture, "A native edit did not reload the projection exactly once.");
                reloads = view.Reloads;
                context.RunOperation(WorkspaceOperationKind.Discard, lease => { fixture.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); });
                Require(!context.State.Dirty && view.Reloads == reloads + 1, fixture, "Discard did not restore the baseline with one native reload.");
                reloads = view.Reloads; view.Pending.Text = "Old mount input";
                fixture.Replace(); ThemeControlSmoke.Pump(host);
                Require(view.Reloads == reloads + 1 && view.Pending.Text == "Loaded native record", fixture, "Replacing a mount with identical native data did not reload exactly once.");
                host.Content = null; ThemeControlSmoke.Pump(host);
                reloads = view.Reloads; availability = view.AvailabilityUpdates;
                context.SetEditedName(name + " unmounted");
                Require(view.Reloads == reloads && view.AvailabilityUpdates == availability, fixture, "An unmounted view retained context subscriptions.");
                context.SetEditedName(name);
                host.Content = view; ThemeControlSmoke.Pump(host);
                Require(view.Reloads > reloads, fixture, "Remounting did not load authoritative native data.");
            }
            finally { host.Close(); ThemeControlSmoke.Pump(host); }
        }
        private static void Require(bool condition, WorkspaceContextCase fixture, string message)
        { if (!condition) throw new InvalidOperationException(fixture.Name + " presentation: " + message); }
    }
}
