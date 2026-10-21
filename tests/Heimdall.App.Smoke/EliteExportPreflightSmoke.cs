using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises blocked native export, cancellation, reviewed repair and correctly named output through the ED window.</summary>
    public static class EliteExportPreflightSmoke
    {
        #region BEGIN Public
        /// <summary>Exports no files until missing references are reviewed and saved; imported and active files stay untouched.</summary>
        public static void Verify(string root, IReadOnlyList<WorkspaceHardware> hardware)
        {
            var physical = new DetectedDcsDevice(Guid.NewGuid(), "Replacement", true, ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "PreflightStore")), new GameSmokeDiscovery(physical));
            var output = Path.Combine(root, "PreflightReview"); var path = Path.Combine(root, "Preflight.4.2.binds");
            const string xml = "<Root PresetName='Custom' MajorVersion='4' MinorVersion='2'><MouseGUI Value='0'/><YawAxisRaw><Binding Device='044FB68F' Key='Joy_XAxis'/><Inverted Value='0'/><Deadzone Value='0.0'/></YawAxisRaw><MouseGUI Value='0'/></Root>";
            File.WriteAllText(path, xml);
            var confirm = false; FakeDeviceInputSource? input = null; IReadOnlyList<GameExportIssue> reviewed = [];
            using var window = new EliteWorkspaceWindow(services, hardware, exportPicker: () => output, pendingChoice: () => MessageBoxResult.No,
                inputFactory: (_, ids) => input = new(ids.Single()), controllerReview: _ => confirm);
            window.ExportReview = issues => { reviewed = issues; return null; };
            window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            Box<TextBox>(window, "ProfileName").Text = "HEIMDALL Flight";
            if (!window.SaveProfile()) throw new InvalidOperationException("Preflight fixture did not save.");
            var before = JsonSerializer.Serialize(window.Session!.Snapshot);
            Click(window, "ExportPresetButton");
            if (reviewed.Count != 1 || !reviewed[0].Assignments.Any(x => x.Contains("Yaw Axis Raw", StringComparison.Ordinal)) || Directory.Exists(output)
                || before != JsonSerializer.Serialize(window.Session.Snapshot)) throw new InvalidOperationException("Cancelled preflight lost affected commands, changed state or wrote files.");
            var dialog = new EliteExportReviewWindow(window, reviewed, [physical], _ => throw new InvalidOperationException("Injected disconnect"), true);
            try
            {
                dialog.Show(); ThemeControlSmoke.Pump(dialog); ThemeControlSmoke.SaveImage(dialog, "elite-export-preflight");
                var selectors = ((DockPanel)dialog.Content).Children.OfType<StackPanel>().Single(x => x.Children.OfType<ComboBox>().Any());
                var target = selectors.Children.OfType<ComboBox>().ElementAt(1); target.SelectedIndex = 0;
                if (target.SelectedItem is not null) throw new InvalidOperationException("Disconnected replacement remained selected in the review dialog.");
            }
            finally { dialog.Close(); }
            window.Activate(); ThemeControlSmoke.Pump(window);
            window.ExportReview = issues => new(issues[0].DeviceId, true);
            Click(window, "ExportPresetButton");
            if (before != JsonSerializer.Serialize(window.Session.Snapshot) || Directory.Exists(output))
                throw new InvalidOperationException("Cancelled clear-impact review changed state or wrote files.");
            window.ExportReview = issues => new(issues[0].DeviceId, false, physical.PhysicalGuid);
            confirm = true; Click(window, "ExportPresetButton");
            if (!window.Session.HasChanges || input?.Disposed != true || Directory.Exists(output))
                throw new InvalidOperationException("Preflight replacement skipped staged-save boundaries or leaked native input handles.");
            if (!window.SaveProfile()) throw new InvalidOperationException("Reviewed repair did not save.");
            Click(window, "ExportPresetButton");
            var exported = Directory.GetFiles(output, "*.binds", SearchOption.AllDirectories).Single();
            var native = XDocument.Load(exported);
            if (Path.GetFileName(exported) != "HEIMDALL Flight.4.2.binds" || (string?)native.Root!.Attribute("PresetName") != "HEIMDALL Flight"
                || native.Root.Elements("MouseGUI").Count() != 1 || (string?)native.Root.Element("YawAxisRaw")!.Element("Binding")!.Attribute("Device") != "044F1001"
                || !Box<TextBlock>(window, "ExportState").Text.Contains("manual", StringComparison.Ordinal) || File.ReadAllText(path) != xml)
                throw new InvalidOperationException("Named game export lost identity, normalization, controller repair, installation guidance or original evidence.");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
