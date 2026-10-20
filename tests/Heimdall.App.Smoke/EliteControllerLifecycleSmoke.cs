using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises remove/replace controls, cancelled reviews, live capability disposal and native export on the actual ED window.</summary>
    public static class EliteControllerLifecycleSmoke
    {
        #region BEGIN Public
        /// <summary>Stages only confirmed controller changes and never writes the imported .binds file.</summary>
        public static void Verify(string root, IReadOnlyList<WorkspaceHardware> hardware)
        {
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Replacement controller", true,
                ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "Lifecycle")), new GameSmokeDiscovery(device));
            var path = Path.Combine(root, "Lifecycle.4.2.binds");
            const string xml = "<Root PresetName='Lifecycle' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='06A30BAC' Key='Joy_1'/><Secondary Device='Keyboard' Key='Key_Space'><Modifier Device='06A30BAC' Key='Joy_2'/></Secondary></PrimaryFire><SecondaryFire><Primary Device='06A30BAC' Key='Joy_9'/><Secondary Device='{NoDevice}' Key=''/></SecondaryFire></Root>";
            File.WriteAllText(path, xml);
            var confirm = false; GameControllerChange? reviewed = null; FakeDeviceInputSource? input = null;
            using var window = new EliteWorkspaceWindow(services, hardware, pendingChoice: () => MessageBoxResult.No,
                inputFactory: (_, ids) => input = new FakeDeviceInputSource(ids.Single()),
                controllerReview: change => { reviewed = change; return confirm; });
            window.Show(); window.Activate(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            var picker = Box<ListBox>(window, "DevicePicker");
            picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Name == "06A30BAC");
            var before = JsonSerializer.Serialize(window.Session!.Snapshot);
            Click(window, "RemoveControllerButton");
            if (reviewed is null || !reviewed.Summary.Contains("cleared: 3", StringComparison.Ordinal) || before != JsonSerializer.Serialize(window.Session.Snapshot))
                throw new InvalidOperationException("Cancelled controller removal changed the workspace or omitted dependent chords.");
            var preview = new ControllerChangeReviewWindow(window, reviewed);
            try { preview.Show(); ThemeControlSmoke.Pump(preview); ThemeControlSmoke.SaveImage(preview, "elite-controller-change-review"); }
            finally { preview.Close(); }
            window.Activate(); ThemeControlSmoke.Pump(window);
            Box<ComboBox>(window, "ConnectedPicker").SelectedItem = device;
            Click(window, "ReplaceControllerButton");
            if (reviewed is null || !reviewed.Summary.Contains("cleared: 1", StringComparison.Ordinal) || input?.Disposed != true
                || before != JsonSerializer.Serialize(window.Session.Snapshot))
                throw new InvalidOperationException("Replacement preview lost cancellation, unsupported-input diagnostics or handle cleanup.");
            confirm = true; Click(window, "ReplaceControllerButton");
            var changed = window.Session.Snapshot;
            if (changed.Profile.Devices.Any(x => x.Identity!.SimSpecificIds["ed.device"] == "06A30BAC")
                || changed.Hardware.Values.Single().Device.PhysicalGuid != device.PhysicalGuid || input?.Disposed != true)
                throw new InvalidOperationException("Confirmed replacement did not transfer the selected controller atomically.");
            var native = window.Session.Export().Files.Values.Single();
            if (native.Contains("06A30BAC", StringComparison.Ordinal) || native.Contains("Joy_9", StringComparison.Ordinal))
                throw new InvalidOperationException("Native export retained obsolete or unsupported controller references.");
            Click(window, "RemoveControllerButton");
            if (window.Session.Snapshot.Hardware.Count != 0 || window.Session.Snapshot.Profile.Devices.Any(x => x.Identity!.SimSpecificIds["ed.device"] != "Keyboard"))
                throw new InvalidOperationException("Controller removal retained hardware pins or removed system input.");
            if (Box<Button>(window, "RemoveControllerButton").IsEnabled || Box<Button>(window, "ReplaceControllerButton").IsEnabled)
                throw new InvalidOperationException("Keyboard/mouse references were offered as removable controllers.");
            if (File.ReadAllText(path) != xml) throw new InvalidOperationException("Controller editing overwrote the imported ED preset.");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
