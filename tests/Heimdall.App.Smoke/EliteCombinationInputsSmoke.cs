using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises device-scoped combination selection, default captions and native-only export on the real editor.</summary>
    public static class EliteCombinationInputsSmoke
    {
        #region BEGIN Public
        /// <summary>Selects an unassigned physical button as a keyboard command's combination, then changes source devices and types an exact key.</summary>
        public static void Verify(string root, IReadOnlyList<WorkspaceHardware> hardware)
        {
            var physical = new DetectedDcsDevice(Guid.NewGuid(), "Combination stick", true,
                ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "CombinationInputs")), new GameSmokeDiscovery(physical));
            var path = Path.Combine(root, "Combinations.4.2.binds");
            File.WriteAllText(path, """
            <Root PresetName="Combinations" MajorVersion="4" MinorVersion="2">
              <PrimaryFire><Primary Device="Keyboard" Key="Key_A"/><Secondary Device="{NoDevice}" Key=""/></PrimaryFire>
              <SecondaryFire><Primary Device="044F1001" Key="Joy_1"/><Secondary Device="{NoDevice}" Key=""/></SecondaryFire>
              <PitchAxisRaw><Binding Device="044F1001" Key="Joy_YAxis"/><Inverted Value="0"/><Deadzone Value="0"/></PitchAxisRaw>
            </Root>
            """);
            using var window = new EliteWorkspaceWindow(services, hardware, pendingChoice: () => MessageBoxResult.No);
            window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            var stick = window.Session!.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "044F1001");
            var keyboard = window.Session.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard");
            var drawing = hardware.Single(x => x.DeviceId == "tm-sol-r-ava-r-buttons");
            window.Session.Associate(stick.LogicalId, drawing.DeviceId, physical.PhysicalGuid);
            var devices = Box<ListBox>(window, "DevicePicker");
            devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(x => x.Id == keyboard.LogicalId);
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
            var commands = Box<ListBox>(window, "CommandsList");
            commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.DisplayName == "Primary Fire");
            var source = Box<ComboBox>(window, "ModifierDevicePicker"); var inputs = Box<ComboBox>(window, "ModifierKey");
            var frozen = JsonSerializer.Serialize(window.Session.Snapshot);
            source.SelectedItem = source.Items.Cast<GameDeviceChoice>().Single(x => x.Id == stick.LogicalId);
            var choice = inputs.Items.Cast<EliteCombinationInput>().Single(x => x.Key == "Joy_9");
            var caption = drawing.Diagram.Controls.First(x => x.InputKey == "JOY_BTN9").Label;
            if (choice.DefaultLabel != caption || !choice.DisplayName.Contains("Button 9", StringComparison.Ordinal)
                || inputs.Items.Cast<EliteCombinationInput>().Any(x => x.Key.EndsWith("Axis", StringComparison.Ordinal))
                || JsonSerializer.Serialize(window.Session.Snapshot) != frozen)
                throw new InvalidOperationException("Combination choices omitted default captions, included axes or changed the profile merely by selecting a source.");
            inputs.SelectedItem = choice; ThemeControlSmoke.Pump(window);
            Box<Expander>(window, "ModifierTools").IsExpanded = true;
            Click(window, "AddCombinationButton"); Click(window, "ApplyAssignmentButton");
            var xml = XDocument.Parse(window.Session.Export().Files.Values.Single());
            var modifier = xml.Root!.Element("PrimaryFire")!.Element("Primary")!.Element("Modifier")!;
            if ((string?)modifier.Attribute("Device") != "044F1001" || (string?)modifier.Attribute("Key") != "Joy_9"
                || !Box<ListBox>(window, "ModifiersList").Items.Cast<object>().Single().ToString()!.Contains(caption, StringComparison.Ordinal)
                || ((GameDeviceChoice)source.SelectedItem).Id != stick.LogicalId)
                throw new InvalidOperationException("Selecting a labeled combination did not export the exact native device/key or retain its default label after refresh.");
            inputs.Text = "Joy_12";
            source.SelectedItem = source.Items.Cast<GameDeviceChoice>().Single(x => x.Id == keyboard.LogicalId);
            if (inputs.Text.Length != 0 || !inputs.Items.Cast<EliteCombinationInput>().Any(x => x.Key == "Key_LeftShift")
                || inputs.Items.Cast<EliteCombinationInput>().Any(x => x.Key == "Joy_9"))
                throw new InvalidOperationException("Changing combination devices retained a stale input or another device's button choices.");
            inputs.Text = "Key_ManualExact"; Click(window, "AddCombinationButton"); Click(window, "ApplyAssignmentButton");
            xml = XDocument.Parse(window.Session.Export().Files.Values.Single());
            if (!xml.Root!.Element("PrimaryFire")!.Element("Primary")!.Elements("Modifier").Any(x =>
                (string?)x.Attribute("Device") == "Keyboard" && (string?)x.Attribute("Key") == "Key_ManualExact"))
                throw new InvalidOperationException("Editable combination choices stopped accepting exact native keys.");
            window.Session.ChangeDrawing(stick.LogicalId, null);
            source.SelectedItem = source.Items.Cast<GameDeviceChoice>().Single(x => x.Id == stick.LogicalId);
            inputs.SelectedItem = inputs.Items.Cast<EliteCombinationInput>().Single(x => x.Key == "Joy_9");
            Box<Expander>(window, "ModifierTools").IsExpanded = true; inputs.BringIntoView();
            ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "elite-combination-inputs");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
