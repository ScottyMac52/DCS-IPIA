using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.App.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Reproduces button/hat records on linked axis-only pedals and verifies capability-aware WPF presentation and editing.</summary>
    public static class SquadronsControllerInputSmoke
    {
        #region BEGIN Public
        /// <summary>Checks native preservation, restricted pickers, failed/obsolete scans and closed-window completion without real hardware.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-pedals-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "T-Pendular-Rudder", true, ProductGuid: Guid.NewGuid());
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map([
                    new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2u, "X axis"),
                    new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u | (1u << 8), "Y axis"),
                    new(Guid.Parse("a36d02e3-c9f3-11cf-bfc7-444553540000"), 2u | (2u << 8), "RZ axis")]);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "store")), discovery);
                using var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No, reviewChange: _ => true);
                window.Show(); ThemeControlSmoke.Pump(window);
                window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
                var native = window.Session!.Export().Files[SquadronsProfileBackend.FileName];
                var devices = Box<ListBox>(window, "DevicePicker");
                devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.3");
                Box<ComboBox>(window, "ConnectedPicker").SelectedItem = device; Click(window, "LinkControllerButton");
                var grid = Box<DataGrid>(window, "BindingsGrid");
                grid.SelectedItem = grid.Items.Cast<SquadronsWorkspaceRow>().Single(x => x.RecordId.EndsWith("ConceptFire.2", StringComparison.Ordinal));
                var row = (SquadronsWorkspaceRow)grid.SelectedItem;
                var inputs = Box<ComboBox>(window, "InputPicker");
                if (row.HardwareStatus != "Not on linked device" || inputs.Items.Count != 6
                    || inputs.Items.Cast<SquadronsInput>().Any(x => x.Type != CommandType.Axis) || Box<Button>(window, "ApplyButton").IsEnabled)
                    throw new InvalidOperationException("Pedals exposed buttons/hats or presented a captured button as a verified control.");
                var targets = Box<ComboBox>(window, "TargetDevice");
                targets.SelectedItem = targets.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.5");
                if (inputs.Items.Count != 0 || inputs.IsEnabled || Box<Button>(window, "ApplyButton").IsEnabled)
                    throw new InvalidOperationException("An unlinked target fell back to generic controls.");
                targets.SelectedItem = targets.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.3");
                if (!window.SaveProfile() || window.Session.Export().Files[SquadronsProfileBackend.FileName] != native)
                    throw new InvalidOperationException("Hardware review changed captured native records.");
                Box<ComboBox>(window, "TypePicker").SelectedItem = "Verified device inputs";
                if (grid.Items.Count != 0) throw new InvalidOperationException("Verified-only filtering retained a nonexistent pedal button.");
                Box<ComboBox>(window, "TypePicker").SelectedItem = "All inputs";
                grid.SelectedItem = grid.Items.Cast<SquadronsWorkspaceRow>().Single(x => x.RecordId.EndsWith("ConceptFire.2", StringComparison.Ordinal));
                discovery.InputScan = () => Task.FromException<IReadOnlyList<ControllerObservation>>(new IOException("Injected scan failure"));
                window.RefreshControllerInputsAsync().GetAwaiter().GetResult();
                if (inputs.Items.Count != 0 || !((SquadronsWorkspaceRow)grid.SelectedItem).HardwareStatus.Contains("unverified", StringComparison.Ordinal))
                    throw new InvalidOperationException("Failed enumeration retained assignable cached controls.");
                var old = new TaskCompletionSource<IReadOnlyList<ControllerObservation>>();
                discovery.InputScan = () => old.Task; var pending = window.RefreshControllerInputsAsync();
                discovery.InputScan = null; window.RefreshControllerInputsAsync().GetAwaiter().GetResult();
                old.SetResult([]); ThemeControlSmoke.Pump(window); pending.GetAwaiter().GetResult();
                if (inputs.Items.Count != 6 || window.Session.HasChanges) throw new InvalidOperationException("An obsolete scan replaced newer capabilities or dirtied the profile.");
                ThemeControlSmoke.SaveImage(window, "squadrons-pedals-input-review");
                var closing = new TaskCompletionSource<IReadOnlyList<ControllerObservation>>();
                discovery.InputScan = () => closing.Task; var finishing = window.RefreshControllerInputsAsync();
                window.Close(); closing.SetResult([]); ThemeControlSmoke.Pump(window); finishing.GetAwaiter().GetResult();
            }
            finally { EditionAccess.SetForTests(previous); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
