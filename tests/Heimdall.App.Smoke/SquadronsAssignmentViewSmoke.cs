using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.App.Games;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks the normal assignment layout and explicit diagnostic/setup surfaces at the reported and minimum window sizes.</summary>
    public static class SquadronsAssignmentViewSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies clean flight defaults, accessible raw records, numeric slot order, full command space and unchanged exports.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-layout-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Smoke Throttle", true, ProductGuid: Guid.NewGuid());
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map([new(Guid.Parse("a36d02e2-c9f3-11cf-bfc7-444553540000"), 2u, "Z axis")]);
                var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
                var menu = source.Split('\n').Where(x => x.Contains("ConceptThrottle.4.", StringComparison.Ordinal))
                    .Select(x => x.Replace("IncomStarshipInputConcepts.ConceptThrottle", "IncomDefaultInputConcepts.ConceptVeryLongSpectatorCameraCommandForWrapping", StringComparison.Ordinal));
                source += string.Join('\n', menu) + "\n";
                var path = Path.Combine(root, "ProfileOptions_profile_synced"); File.WriteAllText(path, source);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "store")), discovery);
                using var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No, reviewChange: _ => true);
                window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
                var review = Box<CheckBox>(window, "ReviewCaptured"); var setup = Box<Expander>(window, "ControllerSetup");
                var section = Box<ComboBox>(window, "SectionPicker"); var grid = Box<DataGrid>(window, "BindingsGrid");
                if (review.IsChecked != false || setup.IsExpanded || section.SelectedItem as string != "Flight" || grid.Items.Count != 0 || !Box<TextBlock>(window, "EmptyState").IsVisible)
                    throw new InvalidOperationException("The normal view opened as a raw-record dump or failed to explain unverified hardware.");
                var devices = Box<ListBox>(window, "DevicePicker");
                if (devices.Items.Cast<GameDeviceChoice>().Select(x => x.Id).Last() != "squadrons.joystick.10")
                    throw new InvalidOperationException("Slot ten appeared before lower-numbered slots.");
                devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.5");
                setup.IsExpanded = true; Box<ComboBox>(window, "ConnectedPicker").SelectedItem = device;
                Box<Button>(window, "LinkControllerButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); setup.IsExpanded = false;
                if (grid.Items.Count != 2 || grid.Items.Cast<SquadronsWorkspaceRow>().Any(x => x.HardwareStatus != "Supported physical input (slot unconfirmed)" || x.Section != "Flight"))
                    throw new InvalidOperationException("Normal flight view included incompatible, unverified or menu records.");
                foreach (var (width, height) in new[] { (1200d, 810d), (900d, 620d) })
                {
                    window.Width = width; window.Height = height; ThemeControlSmoke.Pump(window);
                    if (grid.Columns.Count(x => x.Visibility == Visibility.Visible) != 2 || grid.Columns[0].ActualWidth < 240 || devices.ActualHeight < 240)
                        throw new InvalidOperationException("The selected-controller layout squeezed commands or the controller list.");
                    ThemeControlSmoke.SaveImage(window, "squadrons-assignments-" + (int)width);
                }
                review.IsChecked = true; section.SelectedItem = "All";
                if (grid.Columns.Count(x => x.Visibility == Visibility.Visible) != 6 || !grid.Items.Cast<SquadronsWorkspaceRow>().Any(x => x.Section == "Menus & General"))
                    throw new InvalidOperationException("Capture review failed to restore diagnostics and other contexts.");
                grid.SelectedItem = grid.Items.Cast<SquadronsWorkspaceRow>().Single(x => x.Section == "Menus & General");
                if (((DataGridTextColumn)grid.Columns[0]).ElementStyle.Setters.OfType<Setter>().All(x => x.Property != TextBlock.TextWrappingProperty || !Equals(x.Value, TextWrapping.Wrap)))
                    throw new InvalidOperationException("Long command names cannot wrap.");
                review.IsChecked = false;
                if (grid.Items.Cast<SquadronsWorkspaceRow>().Any(x => x.HardwareStatus != "Supported physical input (slot unconfirmed)")) throw new InvalidOperationException("Leaving review retained raw incompatible records.");
                devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.3");
                if (grid.Items.Count != 0 || Box<StackPanel>(window, "AssignmentEditor").IsVisible)
                    throw new InvalidOperationException("An unlinked controller showed raw controls or an empty editor.");
                if (window.Session!.Export().Files[SquadronsProfileBackend.FileName] != source) throw new InvalidOperationException("Presentation filtering changed native records.");
                window.Close();
            }
            finally { EditionAccess.SetForTests(previous); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        #endregion END private helpers
    }
}
