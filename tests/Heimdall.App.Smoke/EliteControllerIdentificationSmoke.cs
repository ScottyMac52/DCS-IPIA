using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.App;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises unpaired ED discovery, product feedback, explicit confirmation and resource cleanup on the actual window.</summary>
    public static class EliteControllerIdentificationSmoke
    {
        #region BEGIN Public
        /// <summary>Identifies an unknown numeric ED reference by operating hardware before any association exists.</summary>
        public static void Verify(string root, IReadOnlyList<WorkspaceHardware> hardware)
        {
            var target = new DetectedDcsDevice(Guid.NewGuid(), "Unknown physical controller", true,
                ProductGuid: Guid.Parse("0bac06a3-0000-0000-0000-504944564944"));
            var other = new DetectedDcsDevice(Guid.NewGuid(), "Different physical controller", true,
                ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "Identification")), new GameSmokeDiscovery(other, target));
            var path = Path.Combine(root, "Identify.4.2.binds");
            File.WriteAllText(path, "<Root PresetName='Identify' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='06A30BAC' Key='Joy_1'/><Secondary Device='044F1001' Key='Joy_2'/></PrimaryFire></Root>");
            IdentificationInputSource? input = null;
            using var window = new EliteWorkspaceWindow(services, hardware, pendingChoice: () => MessageBoxResult.No,
                inputFactory: (_, ids) =>
                {
                    if (ids.Count != 2 || !ids.Contains(target.PhysicalGuid) || !ids.Contains(other.PhysicalGuid))
                        throw new InvalidOperationException("Identification did not acquire all connected controllers.");
                    return input = new(ids);
                });
            window.Show(); window.Activate(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            var picker = Box<ListBox>(window, "DevicePicker");
            var reference = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Name == "06A30BAC");
            picker.SelectedItem = reference;
            var profile = JsonSerializer.Serialize(window.Session!.Snapshot.Profile);
            Click(window, "ConfirmIdentificationButton");
            if (window.Session.Snapshot.Hardware.Count != 0) throw new InvalidOperationException("Confirmation bypassed identification.");
            AssertActions(window, true, false, false, false, false, true, true, false);
            Click(window, "IdentifyControllerButton");
            AssertActions(window, false, true, false, false, false, false, false, false);
            var identifying = input;
            foreach (var action in new[] { "IdentifyControllerButton", "AddControllerButton", "ReplaceControllerButton", "RemoveControllerButton", "RescanControllerButton", "ChangeDrawingButton" })
                Click(window, action);
            if (!ReferenceEquals(identifying, input) || input!.Disposed || JsonSerializer.Serialize(window.Session.Snapshot.Profile) != profile)
                throw new InvalidOperationException("A disabled device action interrupted identification or changed native data.");
            window.SampleIdentification();
            if (Box<Button>(window, "ConfirmIdentificationButton").IsEnabled || Box<ComboBox>(window, "ConnectedPicker").SelectedItem is not null)
                throw new InvalidOperationException("An idle sample or discovery order selected a controller.");
            input!.Values[other.PhysicalGuid]["JOY_BTN1"] = 1; window.SampleIdentification();
            var result = Box<TextBlock>(window, "IdentificationStatus");
            if (!result.Text.Contains(other.Name, StringComparison.Ordinal) || !result.Text.Contains("Does not match", StringComparison.Ordinal)
                || Box<Button>(window, "ConfirmIdentificationButton").IsEnabled)
                throw new InvalidOperationException("A different product was offered as a matching ED controller.");
            input.Values[target.PhysicalGuid]["JOY_BTN1"] = 1; window.SampleIdentification();
            if (!result.Text.Contains(target.Name, StringComparison.Ordinal) || !result.Text.Contains(target.PhysicalGuid.ToString(), StringComparison.Ordinal)
                || !result.Text.Contains("JOY_BTN1", StringComparison.Ordinal) || !Box<Button>(window, "ConfirmIdentificationButton").IsEnabled
                || window.Session.Snapshot.Hardware.Count != 0)
                throw new InvalidOperationException("Physical activity did not identify the exact unit without automatically linking it.");
            AssertActions(window, false, true, true, false, false, false, false, false);
            ThemeControlSmoke.SaveImage(window, "elite-identify-controller");
            Click(window, "CancelIdentificationButton");
            AssertStopped(window, input);
            AssertActions(window, true, false, false, false, false, true, true, false);
            Box<ComboBox>(window, "ConnectedPicker").SelectedItem = target;
            AssertActions(window, true, false, false, true, true, true, true, false);
            Box<ComboBox>(window, "ConnectedPicker").SelectedItem = null;
            Click(window, "ConfirmIdentificationButton");
            if (window.Session.Snapshot.Hardware.Count != 0) throw new InvalidOperationException("A stopped result could still be confirmed.");

            Click(window, "IdentifyControllerButton"); input!.Values[target.PhysicalGuid]["JOY_X"] = .8; window.SampleIdentification();
            window.Hide(); ThemeControlSmoke.Pump(window); AssertStopped(window, input);
            window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
            Click(window, "IdentifyControllerButton");
            input!.Values[target.PhysicalGuid]["JOY_BTN1"] = 1; input.Values[other.PhysicalGuid]["JOY_BTN1"] = 1;
            window.SampleIdentification();
            if (Box<Button>(window, "ConfirmIdentificationButton").IsEnabled || !result.Text.Contains("Several controllers", StringComparison.Ordinal))
                throw new InvalidOperationException("Simultaneous activity guessed a controller.");
            picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id != reference.Id);
            AssertStopped(window, input);
            picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == reference.Id);
            Click(window, "IdentifyControllerButton"); input!.Fail = true;
            try { window.SampleIdentification(); throw new InvalidOperationException("Disconnect was not reported."); }
            catch (IOException) { AssertStopped(window, input); }

            Click(window, "IdentifyControllerButton");
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1; AssertStopped(window, input!);
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 0;
            Click(window, "IdentifyControllerButton"); input!.Values[target.PhysicalGuid]["JOY_BTN1"] = 1; window.SampleIdentification();
            var acquired = input;
            Click(window, "ConfirmIdentificationButton");
            if (!acquired.Disposed || window.Session.Snapshot.Hardware[reference.Id].Device.PhysicalGuid != target.PhysicalGuid
                || JsonSerializer.Serialize(window.Session.Snapshot.Profile) != profile)
                throw new InvalidOperationException("Confirmed discovery changed native assignments or linked the wrong unit.");
            Box<ComboBox>(window, "ConnectedPicker").SelectedItem = target;
            AssertActions(window, true, false, false, false, false, true, true, true);
            Click(window, "IdentifyControllerButton"); acquired = input!;
            window.Close();
            if (!acquired.Disposed) throw new InvalidOperationException("Closing identification retained controller handles.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void AssertActions(FrameworkElement window, params bool[] expected)
        {
            var names = new[] { "IdentifyControllerButton", "CancelIdentificationButton", "ConfirmIdentificationButton", "AddControllerButton",
                "ReplaceControllerButton", "RemoveControllerButton", "RescanControllerButton", "ChangeDrawingButton" };
            for (var i = 0; i < names.Length; i++)
                if (Box<Button>(window, names[i]).IsEnabled != expected[i])
                    throw new InvalidOperationException("Unexpected device action state: " + names[i]);
            var bar = Box<FeatureActionBar>(window, "FeatureActions");
            foreach (var action in bar.Actions)
                if (window.FindName(action.Id) is Button source && action.Command.CanExecute(null) != source.IsEnabled)
                    throw new InvalidOperationException("The visible action bar did not mirror its device action state.");
        }
        private static void AssertStopped(FrameworkElement window, IdentificationInputSource input)
        {
            var confirmationEnabled = Box<Button>(window, "ConfirmIdentificationButton").IsEnabled;
            var identificationStatus = Box<TextBlock>(window, "IdentificationStatus").Text;
            if (!input.Disposed || confirmationEnabled || identificationStatus.Length != 0)
                throw new InvalidOperationException(
                    "Stopping identification retained native handles or a stale confirmation. " +
                    "Disposed=" + input.Disposed + ", ConfirmEnabled=" + confirmationEnabled +
                    ", IdentificationStatus=" + System.Text.Json.JsonSerializer.Serialize(identificationStatus));
        }
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
