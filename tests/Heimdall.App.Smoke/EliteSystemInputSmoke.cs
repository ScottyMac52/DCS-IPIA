using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Application;
using Heimdall.App.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Uses real keyboard/mouse key regions and illustrated reference previews without physical links.</summary>
    public static class EliteSystemInputSmoke
    {
        #region BEGIN Public
        /// <summary>Selects and edits system inputs using the same diagram click path as physical controller artwork.</summary>
        public static void Verify(string root)
        {
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "SystemInput")), new GameSmokeDiscovery());
            var path = Path.Combine(root, "SystemInput.4.2.binds");
            File.WriteAllText(path, """
            <Root PresetName="SystemInput" MajorVersion="4" MinorVersion="2">
              <PrimaryFire><Primary Device="Keyboard" Key="Key_A"><Modifier Device="Keyboard" Key="Key_LeftShift"/></Primary><Secondary Device="Mouse" Key="Mouse_1"/></PrimaryFire>
              <SecondaryFire><Primary Device="Mouse" Key="Pos_Mouse_ZAxis"/><Secondary Device="{NoDevice}" Key=""/></SecondaryFire>
              <PitchAxisRaw><Binding Device="Mouse" Key="Mouse_XAxis"/><Inverted Value="0"/><Deadzone Value="0"/></PitchAxisRaw>
            </Root>
            """);
            using var window = new EliteWorkspaceWindow(services, [], pendingChoice: () => MessageBoxResult.No);
            window.Show(); window.Activate(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            var picker = Box<ListBox>(window, "DevicePicker");
            picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Name == "Keyboard");
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
            AssertArtwork(window, "Key_A");
            var keyboard = window.Session!.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard");
            // Catalog order starts with Flight Rotation. Select the existing button assignment through its key first.
            Region(window, "Key_A").RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            ThemeControlSmoke.Pump(window);
            if (Box<ListBox>(window, "CommandsList").SelectedItem is not Heimdall.Domain.MultiSim.SimCommand { DisplayName: "Primary Fire", Type: Heimdall.Domain.MultiSim.CommandType.Button }
                || Box<TextBlock>(window, "AssignmentCommand").Text != "Primary Fire"
                || (string?)Box<ComboBox>(window, "InputKey").SelectedValue != "Key_A" || Box<Button>(window, "ApplyAssignmentButton").IsEnabled)
                throw new InvalidOperationException("Clicking an assigned key did not select its unchanged button binding.");
            var region = Region(window, "Key_B");
            region.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            if ((string?)Box<ComboBox>(window, "InputKey").SelectedValue != "Key_B" || !Box<Button>(window, "ApplyAssignmentButton").IsEnabled)
                throw new InvalidOperationException("Clicking a keyboard key did not populate the native assignment draft.");
            Box<Button>(window, "ApplyAssignmentButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!window.Session.Snapshot.Profile.Devices.Single(x => x.LogicalId == keyboard.LogicalId).Buttons.Any(x => x.SourceButton == "Key_B"))
                throw new InvalidOperationException("A clicked keyboard key did not apply through the native assignment boundary.");
            var authored = window.Session.Snapshot.Profile;
            if (!authored.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Mouse").Axes.Any(x => x.SourceAxis == "Mouse_XAxis")
                || authored.Devices.Single(x => x.LogicalId == keyboard.LogicalId).Axes.Count != 0)
                throw new InvalidOperationException("A keyboard button edit changed the existing mouse axis binding.");
            var drawing = Heimdall.EliteDangerous.EliteSystemInputDrawings.Create(authored, keyboard.LogicalId)!;
            var projection = Heimdall.EliteDangerous.EliteWorkspaceProjection.Diagram("Systems", drawing,
                Heimdall.EliteDangerous.EliteWorkspaceProjection.Rows(authored, window.Session.Catalog).Where(x => x.DeviceId == keyboard.LogicalId).ToArray(), "All");
            var composed = Heimdall.Rendering.CompositeKneeboardRenderer.BuildDeviceSvg(drawing, projection);
            if (!composed.Contains("Primary", StringComparison.Ordinal) || !composed.Contains("Fire", StringComparison.Ordinal)
                || composed.Contains("Numbered inputs", StringComparison.Ordinal))
                throw new InvalidOperationException("Keyboard assignment artwork omitted the assigned command name.");
            ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "elite-keyboard-assignments");
            foreach (var token in new[] { "Keyboard", "Mouse" })
            {
                picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Name == token);
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
                AssertArtwork(window, token == "Keyboard" ? "Key_B" : "Mouse_1");
                if (token == "Mouse")
                {
                    Region(window, "Pos_Mouse_ZAxis").RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                    ThemeControlSmoke.Pump(window);
                    if ((string?)Box<ComboBox>(window, "InputKey").SelectedValue != "Pos_Mouse_ZAxis" || Box<TextBlock>(window, "AssignmentCommand").Text != "Secondary Fire")
                        throw new InvalidOperationException("Mouse wheel selection did not show its native input and assigned command in the editor.");
                }
                var frozen = JsonSerializer.Serialize(window.Session.Snapshot);
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 2;
                Box<ComboBox>(window, "ReferenceLayoutPicker").SelectedIndex = 0;
                if (!Box<ComboBoxItem>(window, "ReferenceArtworkOption").IsEnabled || Box<Image>(window, "ReferencePreview").Source is null
                    || !Box<Button>(window, "ReferenceNextButton").IsEnabled)
                    throw new InvalidOperationException("A built-in system drawing did not offer illustrated reference cards and the full table.");
                File.WriteAllBytes(Path.Combine(root, token + "-reference.pdf"), window.ReferencePdf());
                File.WriteAllBytes(Path.Combine(root, token + "-reference.png"), window.ReferencePng());
                var preview = Environment.GetEnvironmentVariable("HEIMDALL_MAPPING_PREVIEW") ?? root;
                Directory.CreateDirectory(preview);
                window.ExportReference(Path.Combine(preview, token + "-artwork.png"));
                if (JsonSerializer.Serialize(window.Session.Snapshot) != frozen || window.Session.Snapshot.Hardware.Count != 0)
                    throw new InvalidOperationException("System artwork or reference export created physical controller links or changed assignments.");
                ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "elite-" + token.ToLowerInvariant() + "-reference");
            }
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static Border Region(FrameworkElement window, string input) => Box<Canvas>(window, "DiagramCanvas").Children.OfType<Border>()
            .Single(x => x.Tag is HardwareGraphicControl control && control.InputKey == input);
        private static void AssertArtwork(FrameworkElement window, string input)
        {
            if (Box<TextBlock>(window, "DiagramHint").Visibility != Visibility.Collapsed || Region(window, input) is null)
                throw new InvalidOperationException("A system device still required a physical hardware drawing.");
            if (Box<Button>(window, "StartInputButton").IsEnabled) throw new InvalidOperationException("System artwork falsely enabled physical input acquisition.");
        }
        #endregion END private helpers
    }
}
