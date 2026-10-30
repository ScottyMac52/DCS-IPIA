using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises adding an omitted physical controller through the shared Devices toolbar without an existing slot selection.</summary>
    internal static class SquadronsAddControllerSmoke
    {
        #region BEGIN Public
        /// <summary>Cancellation, reviewed slot-ten creation, assignment and reopen run unattended with synthetic hardware.</summary>
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-add-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Viper TQS", true, ProductGuid: Guid.NewGuid());
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map(
                    [new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis")]);
                var path = Path.Combine(root, "capture");
                var source = string.Join("", Enumerable.Range(1, 9).Select(x => "GstInput.JoystickDevice" + x + " Existing " + x + " 1\n")) +
                    "GstInput.JoystickDevice10 \nFuture.Setting retained\n";
                File.WriteAllText(path, source);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "store")), discovery);
                var accept = false; var reviews = 0;
                using var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No,
                    reviewChange: _ => { reviews++; return accept; });
                window.Show(); ThemeControlSmoke.Pump(window);
                window.ImportFile(path); ThemeControlSmoke.Pump(window);
                var picker = Box<ListBox>(window, "DevicePicker");
                picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "all");
                var physical = Box<ComboBox>(window, "ConnectedPicker");
                if (!physical.IsEnabled) throw new InvalidOperationException("Adding hardware required selecting an existing captured slot.");
                physical.SelectedItem = device;
                if (Box<TextBox>(window, "NativeControllerName").Text != "Viper TQS 1")
                    throw new InvalidOperationException("The new controller occurrence name was not available for review.");
                var toolbar = Box<FeatureActionBar>(window, "FeatureActions");
                var add = toolbar.Actions.Single(x => x.Id == "AddControllerButton").Command;
                if (!add.CanExecute(null)) throw new InvalidOperationException("The missing-controller action was unavailable.");
                add.Execute(null);
                if (reviews != 1 || window.Session!.HasChanges || window.Session.Export().Files[SquadronsProfileBackend.FileName] != source)
                    throw new InvalidOperationException("Canceling controller creation changed the profile.");
                accept = true; add.Execute(null);
                const string slot = "squadrons.joystick.10";
                if (reviews != 2 || window.Session.Snapshot.Hardware.GetValueOrDefault(slot)?.Device.PhysicalGuid != device.PhysicalGuid ||
                    (picker.SelectedItem as GameDeviceChoice)?.Id != slot)
                    throw new InvalidOperationException("Reviewed addition did not create and select the new linked controller.");
                physical.SelectedItem = device;
                if (Box<Button>(window, "AddControllerButton").IsEnabled)
                    throw new InvalidOperationException("An already linked physical unit could be added again.");
                var backend = new SquadronsProfileBackend();
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Count != 0)
                    throw new InvalidOperationException("Adding a controller adopted native default assignments.");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                var plan = SquadronsDrawingAssignment.For(backend, window.Session.Snapshot.Profile,
                    "controls/IncomStarshipInputConcepts.ConceptThrottle", slot, "JOY_Y", true);
                window.Session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId!, slot, "JOY_Y");
                var rows = backend.ReadBindings(window.Session.Snapshot.Profile);
                if (rows.Count != 2 || rows.Any(x => x.DeviceId != 9 || x.Input?.Key != "JOY_Y"))
                    throw new InvalidOperationException("The added throttle could not receive a verified axis assignment.");
                if (!window.SaveProfile()) throw new InvalidOperationException("The added controller could not be saved.");
                var id = window.Session.Snapshot.Id;
                window.Session.ApplyControllerChange(window.Session.PreviewRenameNativeController(slot, "Viper TQS"));
                if (!window.SaveProfile()) throw new InvalidOperationException("The legacy bare-name fixture could not be saved.");
                window.Close();
                var repair = false;
                using var reopened = new SquadronsWorkspaceWindow(services, id, () => MessageBoxResult.No, _ => repair);
                reopened.Show(); ThemeControlSmoke.Pump(reopened);
                var reopenedPicker = Box<ListBox>(reopened, "DevicePicker");
                reopenedPicker.SelectedItem = reopenedPicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == slot);
                if (Box<TextBox>(reopened, "NativeControllerName").Text != "Viper TQS 1")
                    throw new InvalidOperationException("A previously saved bare name had no occurrence-name repair suggestion.");
                var rename = Box<FeatureActionBar>(reopened, "FeatureActions").Actions.Single(x => x.Id == "RenameControllerButton").Command;
                if (!rename.CanExecute(null)) throw new InvalidOperationException("The existing native name could not be repaired.");
                var bare = reopened.Session!.Export().Files[SquadronsProfileBackend.FileName];
                rename.Execute(null);
                if (reopened.Session.Export().Files[SquadronsProfileBackend.FileName] != bare)
                    throw new InvalidOperationException("Canceling a native name update changed the draft.");
                repair = true; rename.Execute(null);
                if (reopened.Session.Snapshot.Profile.Devices.Single(x => x.LogicalId == slot).Identity!.WindowsName != "Viper TQS 1" ||
                    reopened.Session.Snapshot.Hardware[slot].Device.PhysicalGuid != device.PhysicalGuid ||
                    backend.ReadBindings(reopened.Session.Snapshot.Profile).Count != 2)
                    throw new InvalidOperationException("Native name repair changed the physical link or assignments.");
                if (!reopened.SaveProfile()) throw new InvalidOperationException("The repaired native name could not be saved.");
                reopened.Close();
            }
            finally { EditionAccess.SetForTests(previous); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        #endregion END private helpers
    }
}
