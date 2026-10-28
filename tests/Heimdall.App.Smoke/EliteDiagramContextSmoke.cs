using System.Globalization;
using System.Text.Json;
using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Smoke
{
    /// <summary>Regresses callout actions while an unrelated command is selected.</summary>
    internal static class EliteDiagramContextSmoke
    {
        internal static void Verify(EliteWorkspaceWindow window)
        {
            var session = window.Session!;
            var frozen = JsonSerializer.Serialize(session.Snapshot.Profile);
            var canvas = (Canvas)window.FindName("DiagramCanvas");
            var controls = canvas.Children.OfType<Border>().Select(x => x.Tag).OfType<HardwareGraphicControl>().ToArray();
            var button = controls.First(x => !x.IsAxis && EliteInputKeys.FromShared(x.InputKey) == "Joy_9");
            var axis = controls.First(x => x.IsAxis && EliteInputKeys.FromShared(x.InputKey) == "Joy_YAxis");
            var commands = (ListBox)window.FindName("CommandsList");
            var actions = window.DiagramInteractions();
            try
            {
                commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Pitch Axis Raw");
                if (actions.CanClear?.Invoke(button) != true || actions.CanTune?.Invoke(button) != false)
                    throw new InvalidOperationException("Button callout actions depend on a prior left-click or offer axis tuning.");
                actions.Clear!(button);
                if (session.Snapshot.Profile.Devices.SelectMany(x => x.Buttons).Any(x => x.SourceButton == "Joy_9"))
                    throw new InvalidOperationException("Callout Clear did not clear the pointed button.");
                session.Discard();
                if (JsonSerializer.Serialize(session.Snapshot.Profile) != frozen)
                    throw new InvalidOperationException("Discard did not restore the context-cleared binding.");

                commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Primary Fire");
                if (actions.CanClear?.Invoke(axis) != true || actions.CanTune?.Invoke(axis) != true)
                    throw new InvalidOperationException("Axis callout actions require a prior selection.");
                window.AxisTuningReview = dialog =>
                {
                    foreach (var invalid in new[] { "NaN", "Infinity", "-1", "101", "invalid" })
                    {
                        dialog.Deadzone.Text = invalid;
                        if (dialog.TryRead(out _, out _)) throw new InvalidOperationException("Axis tuning accepted invalid native values.");
                    }
                    foreach (var boundary in new[] { 0, 100 })
                    {
                        dialog.Deadzone.Text = boundary.ToString(CultureInfo.CurrentCulture);
                        if (!dialog.TryRead(out _, out _)) throw new InvalidOperationException("Axis tuning rejected a valid percentage boundary.");
                    }
                    dialog.Invert.IsChecked = true;
                    dialog.Deadzone.Text = 12.5f.ToString(CultureInfo.CurrentCulture);
                    return false;
                };
                actions.Tune!(axis);
                if (JsonSerializer.Serialize(session.Snapshot.Profile) != frozen)
                    throw new InvalidOperationException("Cancelling axis tuning changed the native profile.");

                commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Primary Fire");
                window.AxisTuningReview = dialog =>
                {
                    dialog.Invert.IsChecked = true;
                    dialog.Deadzone.Text = 12.5f.ToString(CultureInfo.CurrentCulture);
                    return true;
                };
                actions.Tune!(axis);
                var native = new EliteDangerousProfileBackend().Import("controls", session.Export().Files);
                if (!native.Devices.SelectMany(x => x.Axes).Any(x => x.Invert && x.Deadzone == 12.5f))
                    throw new InvalidOperationException("Callout axis tuning did not survive native Elite export.");
                if (!session.Snapshot.Profile.Devices.SelectMany(x => x.Buttons).Any(x => x.SourceButton == "Joy_9"))
                    throw new InvalidOperationException("Tuning the pointed axis changed the unrelated button.");
            }
            finally
            {
                window.AxisTuningReview = null;
                session.Discard();
            }
        }
    }
}
