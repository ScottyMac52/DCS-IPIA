using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises reviewed physical links and shared identification on the actual Squadrons desktop window.</summary>
    public static class SquadronsDeviceLinkSmoke
    {
        #region BEGIN Public
        /// <summary>Uses duplicate synthetic devices and injectable input; no installed game or connected hardware is needed.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-link-ui-" + Guid.NewGuid().ToString("N"));
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var first = new DetectedDcsDevice(Guid.NewGuid(), "Viper TQS", true, ProductGuid: Guid.NewGuid());
                var second = first with { PhysicalGuid = Guid.NewGuid() };
                var discovery = new GameSmokeDiscovery(first, second);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(root), discovery);
                IdentificationInputSource? input = null; var accept = false; GameControllerChange? reviewed = null;
                var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No,
                    reviewChange: change => { reviewed = change; return accept; }, identificationFactory: (_, ids) => input = new(ids));
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
                Box<Expander>(window, "ControllerSetup").IsExpanded = true;
                window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
                var native = window.Session!.Export().Files[SquadronsProfileBackend.FileName];
                if (Box<Button>(window, "LinkControllerButton").IsEnabled) throw new InvalidOperationException("All-devices filter allowed physical linking.");
                SelectSlot(window, 5);
                Click(window, "IdentifyControllerButton"); window.SampleIdentification();
                var picker = Box<ComboBox>(window, "ConnectedPicker");
                if (picker.SelectedItem is not null || Box<Button>(window, "LinkControllerButton").IsEnabled)
                    throw new InvalidOperationException("Idle discovery guessed a physical unit.");
                input!.Values[first.PhysicalGuid]["JOY_BTN1"] = 1; input.Values[second.PhysicalGuid]["JOY_BTN1"] = 1;
                window.SampleIdentification();
                if (picker.SelectedItem is not null || !Box<TextBlock>(window, "IdentificationStatus").Text.Contains("Several", StringComparison.Ordinal))
                    throw new InvalidOperationException("Simultaneous activity guessed a physical unit.");
                input.Values[first.PhysicalGuid]["JOY_X"] = .8; window.SampleIdentification();
                if (!input.Disposed || (picker.SelectedItem as DetectedDcsDevice)?.PhysicalGuid != first.PhysicalGuid)
                    throw new InvalidOperationException("Identification failed to select and release the exact unit.");
                Click(window, "LinkControllerButton");
                if (window.Session.Snapshot.Hardware.Count != 0 || reviewed is null) throw new InvalidOperationException("Canceled link review staged changes.");
                if ((picker.SelectedItem as DetectedDcsDevice)?.PhysicalGuid != first.PhysicalGuid ||
                    !Box<Button>(window, "LinkControllerButton").IsEnabled || !input.Disposed)
                    throw new InvalidOperationException("Canceled link review discarded the chosen physical unit or retained identification handles.");
                accept = true; Click(window, "LinkControllerButton");
                if (window.Session.Snapshot.Hardware["squadrons.joystick.5"].Device.PhysicalGuid != first.PhysicalGuid || !window.SaveProfile())
                    throw new InvalidOperationException("Reviewed physical link failed to save.");
                SelectSlot(window, 10); picker.SelectedItem = second; Click(window, "LinkControllerButton");
                if (window.Session.Snapshot.Hardware.Count != 2) throw new InvalidOperationException("Duplicate product did not retain independently chosen slots.");
                SelectSlot(window, 5); picker.SelectedItem = second; Click(window, "LinkControllerButton");
                if (window.Session.Snapshot.Hardware["squadrons.joystick.5"].Device.PhysicalGuid != first.PhysicalGuid)
                    throw new InvalidOperationException("One physical unit was linked to two slots.");
                SelectSlot(window, 10); accept = false; Click(window, "UnlinkControllerButton");
                if (window.Session.Snapshot.Hardware.Count != 2) throw new InvalidOperationException("Canceled unlink removed a physical link.");
                accept = true; Click(window, "UnlinkControllerButton");
                SelectSlot(window, 5);
                if (picker.IsEnabled || (picker.SelectedItem as DetectedDcsDevice)?.PhysicalGuid != first.PhysicalGuid)
                    throw new InvalidOperationException("Saved physical link is not displayed and locked.");
                Click(window, "UnlinkControllerButton");
                if (!picker.IsEnabled) throw new InvalidOperationException("Explicit unlink did not unlock the physical selector.");
                picker.SelectedItem = second; Click(window, "LinkControllerButton");
                if (window.Session.Snapshot.Hardware["squadrons.joystick.5"].Device.PhysicalGuid != second.PhysicalGuid
                    || window.Session.Export().Files[SquadronsProfileBackend.FileName] != native)
                    throw new InvalidOperationException("Replacement lost review identity or changed native bindings.");
                Click(window, "DiscardButton");
                if (window.Session.Snapshot.Hardware["squadrons.joystick.5"].Device.PhysicalGuid != first.PhysicalGuid)
                    throw new InvalidOperationException("Discard failed to restore the saved association.");
                SelectSlot(window, 10); Click(window, "IdentifyControllerButton");
                window.Hide(); ThemeControlSmoke.Pump(window);
                if (!input!.Disposed) throw new InvalidOperationException("Focus loss retained controller handles.");
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
                Click(window, "IdentifyControllerButton"); SelectSlot(window, 5);
                if (!input!.Disposed) throw new InvalidOperationException("Slot switching retained controller handles.");
                SelectSlot(window, 10); Click(window, "IdentifyControllerButton"); window.Close();
                if (!input!.Disposed) throw new InvalidOperationException("Closing retained controller handles.");
                var reopened = new SquadronsWorkspaceWindow(services, window.Session.Snapshot.Id, () => MessageBoxResult.No, _ => true);
                reopened.Show(); ThemeControlSmoke.Pump(reopened); SelectSlot(reopened, 5);
                discovery.Devices = []; Click(reopened, "RescanControllerButton");
                if (!Box<TextBlock>(reopened, "LinkStatus").Text.Contains("Unavailable", StringComparison.Ordinal) || Box<Button>(reopened, "IdentifyControllerButton").IsEnabled)
                    throw new InvalidOperationException("Disconnected hardware was presented as ready.");
                var savedPicker = Box<ComboBox>(reopened, "ConnectedPicker");
                if (savedPicker.IsEnabled || (savedPicker.SelectedItem as DetectedDcsDevice)?.PhysicalGuid != first.PhysicalGuid)
                    throw new InvalidOperationException("Offline saved identity was cleared or unlocked.");
                Click(reopened, "UnlinkControllerButton");
                if (reopened.Session!.Snapshot.Hardware.Count != 0 || reopened.Session.Export().Files[SquadronsProfileBackend.FileName] != native)
                    throw new InvalidOperationException("Offline unlink deleted assignments or failed to remove the association.");
                ThemeControlSmoke.SaveImage(reopened, "squadrons-device-links"); reopened.Close();
            }
            finally { EditionAccess.SetForTests(previous); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void SelectSlot(FrameworkElement window, int slot)
        {
            var picker = Box<ListBox>(window, "DevicePicker"); picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick." + slot);
        }
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
