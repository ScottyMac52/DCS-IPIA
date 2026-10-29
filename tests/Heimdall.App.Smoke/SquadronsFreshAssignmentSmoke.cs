using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises drawing assignment from a fresh game without a binding table or imported templates.</summary>
    internal static class SquadronsFreshAssignmentSmoke
    {
        #region BEGIN Public
        /// <summary>Uses the real workspace drop route and verifies isolated, persisted paired-axis creation.</summary>
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-fresh-smoke-" + Guid.NewGuid().ToString("N"));
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Fresh stick", true, ProductGuid: Guid.NewGuid());
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map(
                    [new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis")]);
                var axis = new HardwareGraphicControl("axis", "Y axis", "JOY_Y", 10, 10, 100, 40, true);
                var drawing = new UiLayerDiagram("Synthetic", null, "fixture", 200, 200, [], [])
                {
                    Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"200\" viewBox=\"0 0 200 200\"/>",
                    Controls = [axis]
                };
                var hardware = new WorkspaceHardware("fresh", "Fresh drawing", "fixture.lua", drawing);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(root), discovery);
                using var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No, drawings: [hardware]);
                window.Show(); ThemeControlSmoke.Pump(window);
                window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Fresh_profile_synced"));
                var backend = new SquadronsProfileBackend();
                if (backend.ReadBindings(window.Session!.Snapshot.Profile).Count != 0)
                    throw new InvalidOperationException("Importing a fresh profile invented active assignments.");
                const string slot = "squadrons.joystick.2";
                var picker = Box<ListBox>(window, "DevicePicker");
                picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == slot);
                Box<ComboBox>(window, "ConnectedPicker").SelectedItem = device;
                Box<Button>(window, "LinkControllerButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Box<ComboBox>(window, "DrawingPicker").SelectedItem = hardware;
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
                ThemeControlSmoke.Pump(window);
                var commands = (ListBox)Box<GameCommandBrowserView>(window, "CommandBrowser").FindName("CommandsList");
                var pitch = commands.Items.Cast<SimCommand>().Single(x => x.Metadata["squadrons.concept"] == "IncomStarshipInputConcepts.ConceptPitch");
                window.DrawingInteractions().Drop!(axis, pitch.Id);
                var rows = backend.ReadBindings(window.Session.Snapshot.Profile);
                if (rows.Count != 2 || rows.Any(x => x.Concept != pitch.Metadata["squadrons.concept"] || x.DeviceId != 1 || x.Input?.Key != "JOY_Y"))
                    throw new InvalidOperationException("Fresh drawing drop failed to create the requested axis pair.");
                if (!window.SaveProfile()) throw new InvalidOperationException("Fresh axis assignment could not be saved.");
                var id = window.Session.Snapshot.Id;
                window.Close();
                using var reopened = new SquadronsWorkspaceWindow(services, id, () => MessageBoxResult.No, drawings: [hardware]);
                if (backend.ReadBindings(reopened.Session!.Snapshot.Profile).Count != 2)
                    throw new InvalidOperationException("Fresh axis assignment did not survive workspace reopen.");
            }
            finally { EditionAccess.SetForTests(previous); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        #endregion END private helpers
    }
}
