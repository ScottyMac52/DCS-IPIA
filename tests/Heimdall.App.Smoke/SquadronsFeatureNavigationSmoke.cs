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
    /// <summary>Regresses shared navigation, feature-owned actions and foreground-only live testing.</summary>
    internal static class SquadronsFeatureNavigationSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies actual mounted views, toolbar routing and input lifecycle with synthetic controllers.</summary>
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-features-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Synthetic stick", true, ProductGuid: Guid.NewGuid());
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 4u, "Button 1")]);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(root), discovery);
                IdentificationInputSource? source = null;
                using var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No,
                    reviewChange: _ => true, drawings: [], inputFactory: (_, ids) => source = new(ids));
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
                var navigation = Box<ListBox>(window, "FeaturePicker");
                var content = Box<ContentControl>(window, "FeatureContent");
                var toolbar = Box<FeatureActionBar>(window, "FeatureActions");
                if (navigation.SelectedIndex != 0 || content.Content is not SquadronsDevicesView devices || !devices.IsVisible ||
                    !toolbar.Actions.Any(x => x.Id == "IdentifyControllerButton") || toolbar.Actions.Any(x => x.Id == "StartInputButton"))
                    throw new InvalidOperationException("Squadrons did not open on the shared Devices feature with device-owned actions.");
                window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
                var picker = Box<ListBox>(window, "DevicePicker");
                picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.3");
                Box<ComboBox>(window, "ConnectedPicker").SelectedItem = device;
                toolbar.Actions.Single(x => x.Id == "LinkControllerButton").Command.Execute(null);
                if (!window.SaveProfile()) throw new InvalidOperationException("Linked fixture did not save.");
                var native = window.Session!.Export().Files[SquadronsProfileBackend.FileName];
                navigation.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                if (content.Content is not SquadronsAssignmentsView assignments || !assignments.IsVisible || devices.IsVisible ||
                    !toolbar.Actions.Any(x => x.Id == "StartInputButton") || toolbar.Actions.Any(x => x.Id == "LinkControllerButton"))
                    throw new InvalidOperationException("Feature navigation failed to replace the device surface and its toolbar.");
                var browser = Box<GameCommandBrowserView>(window, "CommandBrowser");
                var search = (TextBox)browser.FindName("CommandSearch"); search.Text = "Fire";
                toolbar.Actions.Single(x => x.Id == "StartInputButton").Command.Execute(null);
                if (source is null || source.Disposed) throw new InvalidOperationException("The shared Start input action did not acquire the linked controller.");
                source.Values[device.PhysicalGuid]["JOY_BTN1"] = 1;
                window.SampleInput();
                if (!Box<TextBox>(window, "InputActivity").IsVisible || !Box<TextBox>(window, "InputActivity").Text.Contains("JOY_BTN1", StringComparison.Ordinal))
                    throw new InvalidOperationException("Table-only input testing did not display live controller activity.");
                var staleStart = toolbar.Actions.Single(x => x.Id == "StartInputButton").Command;
                navigation.SelectedIndex = 0; ThemeControlSmoke.Pump(window);
                if (!source.Disposed || Box<TextBox>(window, "InputActivity").Text.Length != 0 || staleStart.CanExecute(null))
                    throw new InvalidOperationException("Feature navigation retained native handles, activity or a stale action command.");
                navigation.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                if (!ReferenceEquals(assignments, content.Content) || search.Text != "Fire")
                    throw new InvalidOperationException("Cached assignment view lost its filters while navigating.");
                toolbar.Actions.Single(x => x.Id == "StartInputButton").Command.Execute(null);
                picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "system.2");
                if (source is null || !source.Disposed || Box<Button>(window, "StartInputButton").IsEnabled)
                    throw new InvalidOperationException("Switching to a read-only system source kept live input active.");
                picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.3");
                toolbar.Actions.Single(x => x.Id == "StartInputButton").Command.Execute(null);
                var foreground = new Window { Width = 200, Height = 120, ShowInTaskbar = false };
                foreground.Show(); foreground.Activate(); ThemeControlSmoke.Pump(foreground);
                foreground.Close();
                if (source is null || !source.Disposed) throw new InvalidOperationException("Focus loss retained native input handles.");
                if (window.Session.Export().Files[SquadronsProfileBackend.FileName] != native || window.Session.HasChanges)
                    throw new InvalidOperationException("Navigation or raw input testing changed native profile evidence.");
                window.Close();
            }
            finally { EditionAccess.SetForTests(previous); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        #endregion END private helpers
    }
}
