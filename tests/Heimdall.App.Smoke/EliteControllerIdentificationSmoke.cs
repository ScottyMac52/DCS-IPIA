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
            Click(window, "IdentifyControllerButton");
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
            ThemeControlSmoke.SaveImage(window, "elite-identify-controller");
            Click(window, "CancelIdentificationButton");
            AssertStopped(window, input);
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
            Click(window, "IdentifyControllerButton"); acquired = input!;
            window.Close();
            if (!acquired.Disposed) throw new InvalidOperationException("Closing identification retained controller handles.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void AssertStopped(FrameworkElement window, IdentificationInputSource input)
        {
            if (!input.Disposed || Box<Button>(window, "ConfirmIdentificationButton").IsEnabled || Box<TextBlock>(window, "IdentificationStatus").Text.Length != 0)
                throw new InvalidOperationException("Stopping identification retained native handles or a stale confirmation.");
        }
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
